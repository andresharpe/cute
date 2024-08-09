import os
import subprocess

def import_or_install(package):
    try:
        __import__(package)
        return 'Package {package} is already installed'.format(package=package)
    except ImportError:
        print('Package {package} not found. Installing {package}...'.format(package=package))
        subprocess.run(['pip', 'install', package])
        return 'Package {package} is installed'.format(package=package)

# move to install
#import_or_install('requests')
#import_or_install('bs4')
#import_or_install('nltk')
#import_or_install('hlepor')
#import_or_install('deepeval')
#import_or_install('nptyping')

# Set the Azure OpenAI credentials for the deepeval package
subprocess.run(["deepeval", "set-azure-openai" ,
                "--openai-api-key", settings['Cute__OpenAiApiKey'], 
                "--openai-endpoint", settings['Cute__OpenAiEndpoint'], 
                "--openai-api-version", '2024-07-01-preview', 
                "--deployment-name", settings['Cute__OpenAiDeploymentName']
            ])
