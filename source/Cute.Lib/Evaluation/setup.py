import pip

def import_or_install(package):
    try:
        __import__(package)
    except ImportError:
        pip.main(['install', '--user', package])

import_or_install('deepeval')
