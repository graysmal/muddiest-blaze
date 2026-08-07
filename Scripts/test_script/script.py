import json
# this is test_script.py

with open('params.json', 'r') as f:
    params = json.load(f)

print(params)

print(params['example_string'])

def hello_world():
    print('hello world')

print('script ran')

with open('test.txt', 'w') as file:
    file.write('testing!')