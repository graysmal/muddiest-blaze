import json
with open('params.json', 'r') as f:
    params = json.load(f)

with open(f'{params['filename']}', 'w') as f:
    f.write('hello world!')
print(f'\'hello world!\' saved to {params['filename']}')
