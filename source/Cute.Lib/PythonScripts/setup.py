import pip

def import_or_install(package):
    try:
        __import__(package)
        return 'Package {package} is already installed'.format(package=package)
    except ImportError:
        pip.main(['install', '--user', package])
        return 'Package {package} is installed'.format(package=package)

print(import_or_install('deepeval'))
print(import_or_install('json'))
