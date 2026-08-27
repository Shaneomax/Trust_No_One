import re

with open(r'Assets\V0\Scene\GameScene.unity', 'r', encoding='utf-8') as f:
    text = f.read()

pl_tr_id = "1149575093"

m_children = re.search(r'--- !u!4 &' + pl_tr_id + r'\nTransform:.*?\n  m_Children:(.*?)(?=\n  m_Father:)', text, re.DOTALL)
child_tr_ids = re.findall(r'- \{fileID: (\d+)\}', m_children.group(1))

print(f'PointLights has {len(child_tr_ids)} children')

for c_tr_id in child_tr_ids:
    c_tr_match = re.search(r'--- !u!4 &' + c_tr_id + r'\nTransform:(.*?)(?=\n---|\Z)', text, re.DOTALL)
    if not c_tr_match:
        print(f'Tr {c_tr_id} not found')
        continue
    c_tr = c_tr_match.group(1)
    go_id_m = re.search(r'm_GameObject: \{fileID: (\d+)\}', c_tr)
    if not go_id_m:
        continue
    go_id = go_id_m.group(1)
    pos_m = re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', c_tr)
    pos = pos_m.groups() if pos_m else '?'
    go_block_m = re.search(r'--- !u!1 &' + go_id + r'\nGameObject:(.*?)(?=\n---|\Z)', text, re.DOTALL)
    name = re.search(r'm_Name: ([^\n]+)', go_block_m.group(1)).group(1) if go_block_m else '?'
    
    # Check components
    comps = re.findall(r'- component: \{fileID: (\d+)\}', go_block_m.group(1)) if go_block_m else []
    print(f'GO {go_id} ({name}) at {pos} with comps: {comps}')
