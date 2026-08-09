import json
with open('params.json', 'r') as f:
    params = json.load(f)

print(params['text'])
