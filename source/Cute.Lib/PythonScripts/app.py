import os
import sys
import subprocess

from flask import Flask, request, jsonify, json
from pydantic.json import pydantic_encoder

from eval_generation import EvalGeneration
from eval_translation import EvalTranslation
from eval_seo import EvalSeo

app = Flask(__name__)

def ensure_open_ai_is_used(env):
    subprocess.run(["deepeval", "set-azure-openai" ,
                "--openai-api-key", env['Cute__OpenAiApiKey'], 
                "--openai-endpoint", env['Cute__OpenAiEndpoint'], 
                "--deployment-name", env['Cute__OpenAiDeploymentName'],
                "--openai-api-version", '2024-07-01-preview'
            ])
    ensure_open_ai_is_used.__code__ = (lambda env: None).__code__


@app.route('/api/generator/<string:measure>', methods=['POST'])
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


@app.route('/api/translator/<string:measure>', methods=['POST'])
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


@app.route('/api/seo', methods=['POST'])
def execute_seo_command():

    payload = request.json

    options = payload['options']

    env = payload['env']
    
    seoEvaluator = EvalSEO(
                options['seo-input-method'], 
                options['keyword'], 
                options['related-keywords'],
                options['threshold'],
            )

    result = seoEvaluator.measure()

    return json.dumps(result, default=pydantic_encoder)


