import re

with open(r'Assets\V0\Scene\GameScene.unity', 'r', encoding='utf-8') as f:
    text = f.read()

house_tr_id = "2068229373"

m_tr = re.search(r'--- !u!4 &' + house_tr_id + r'\nTransform:(.*?)(?=\n---|\Z)', text, re.DOTALL)
b = m_tr.group(1)
pos = re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', b)
print('House pos:', pos.groups() if pos else 'none')

m_children = re.search(r'm_Children:(.*?)(?=\n  m_Father:)', b, re.DOTALL)
children = re.findall(r'- \{fileID: (\d+)\}', m_children.group(1))
print(f'House has {len(children)} direct children')

xs, ys, zs = [], [], []
for cid in children[:50]:
    c_tr = re.search(r'--- !u!4 &' + cid + r'\nTransform:.*?\n  m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}', text, re.DOTALL)
    if c_tr:
        xs.append(float(c_tr.group(1)))
        ys.append(float(c_tr.group(2)))
        zs.append(float(c_tr.group(3)))

if xs:
    print(f'Local X range: {min(xs)} to {max(xs)}')
    print(f'Local Y range: {min(ys)} to {max(ys)}')
    print(f'Local Z range: {min(zs)} to {max(zs)}')
