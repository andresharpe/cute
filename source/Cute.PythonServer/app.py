"""
This script runs the application using a development server.
It contains the definition of routes and views for the application.
"""

import os
import subprocess

from apiflask import APIFlask
from flask import request, json
from pydantic.json import pydantic_encoder

from eval_generation import EvalGeneration
from eval_seo import EvalSeo
from eval_translation import EvalTranslation

app = APIFlask(__name__, title="Cute Python Server")

# Make the WSGI interface available at the top level so wfastcgi can get it.
wsgi_app = app.wsgi_app


@app.get('/')
def index():
    return "Cute Python Server"

@app.get('/healthz')
def health_check():
    return "Healthy"

@app.post('/api/generator/<string:measure>')
def execute_generator_command(measure:str):

    payload = request.json

    options = payload['options']

    env = payload['env']

    ensure_open_ai_is_used(env)
    
    generator = EvalGeneration(
                options['llm-model'], 
                options['threshold'], 
                options['prompt-field'],
                options['generated-content'],
                options['reference-content'],
                options['facts']
            )

    match measure:

        case 'all':
            result = generator.evaluate()

        case 'answer' | 'faithfulness':
            result = generator.measure(measure)

        case _:
            return "Invalid generator option", 400

    return json.dumps(result, default=pydantic_encoder)


@app.post('/api/translator/<string:measure>')
def execute_translator_command(measure:str):

    payload = request.json

    options = payload['options']

    env = payload['env']
    
    translator = EvalTranslation(
                options['llm-model'], 
                options['threshold'], 
                options['prompt-field'],
                options['generated-content'],
                options['reference-content'],
            )

    match measure:

        case 'all':
            result = translator.evaluate()

        case 'gleu' | 'meteor' | 'lepor':
            result = translator.measure(measure)

        case _:
            return "Invalid translator option", 400

    return json.dumps(result, default=pydantic_encoder)


@app.post('/api/seo')
def execute_seo_command():

    payload = request.json

    options = payload['options']

    seoEvaluator = EvalSeo(
                options['seo-input-method'], 
                options['keyword'], 
                options['related-keywords'],
                options['threshold'],
            )

    result = seoEvaluator.measure()

    return json.dumps(result, default=pydantic_encoder)


def ensure_open_ai_is_used(env):
    subprocess.run(["deepeval", "set-azure-openai" ,
                "--openai-api-key", env['Cute__OpenAiApiKey'], 
                "--openai-endpoint", env['Cute__OpenAiEndpoint'], 
                "--deployment-name", env['Cute__OpenAiDeploymentName'],
                "--openai-api-version", '2024-07-01-preview'
            ])
    
    # ensure code preceding this is run only once
    ensure_open_ai_is_used.__code__ = (lambda env: None).__code__


if __name__ == '__main__':
    import os
    HOST = os.environ.get('SERVER_HOST', 'localhost')
    try:
        PORT = int(os.environ.get('SERVER_PORT', '5555'))
    except ValueError:
        PORT = 5555
    app.run(HOST, PORT, debug=True)
