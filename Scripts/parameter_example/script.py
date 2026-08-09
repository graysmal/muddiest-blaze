import json
with open('params.json', 'r') as f:
    params = json.load(f)

print(f'example_string: {params['example_string']}')
print(f'example_int * 2: {params['example_int'] * 2}')
print(f'example_bool: {params['example_bool']}')
print(f'example_float / 2: {params['example_float'] / 2}')
print('\nexample_list_string:')
for str in params['example_list_string']:
    print(str)
print('\nexample_list_int:')
for i in params['example_list_int']:
    print(i)
print('\nexample_list_bool:')
for boo in params['example_list_bool']:
    print(boo)
print('\nexample_list_float:')
for flo in params['example_list_float']:
    print(str)
print('\nexample_list_datetime:')
for dt in params['example_list_datetime']:
    print(dt)
print('\nexample_list_2D:')
for x, a in enumerate(params['example_list_2D']):
    print(f'\t{x}')
    for str in a:
        print(f'\t\t{str}')
