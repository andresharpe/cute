import os
import subprocess
import pip

def import_or_install(package):
    try:
        __import__(package)
        return 'Package {package} is already installed'.format(package=package)
    except ImportError:
        pip.main(['install', '--user', package])
        print('Package {package} not found. Installing {package}...'.format(package=package))
        return 'Package {package} is installed'.format(package=package)

import_or_install('dotenv')
import_or_install('requests')
import_or_install('bs4')
import_or_install('nltk')
import_or_install('hlepor')
import_or_install('deepeval')

# Set the Azure OpenAI credentials for the deepeval package
from dotenv import load_dotenv
load_dotenv()
subprocess.run(["deepeval", "set-azure-openai" ,
                "--openai-api-key", os.getenv('Cute__OpenaiApiKey'), 
                "--openai-endpoint", os.getenv('Cute__OpenaiEndpoint'), 
                "--openai-api-version", os.getenv('Cute__OpenaiApiVersion'), 
                "--deployment-name", os.getenv('Cute__DeploymentName'),
                "--model-version", os.getenv('Cute__ModelVersion')])
