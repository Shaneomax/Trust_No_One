import re

scene_path = r"Assets\V0\Scene\GameScene.unity"
with open(scene_path, "r", encoding="utf-8") as f:
    text = f.read()

# Map Transform id to {go_id, father_id, pos, rot}
transforms = {}
for m in re.finditer(r"--- !u!4 &(\d+)\nTransform:(.*?)(?=\n---|\Z)", text, re.DOTALL):
    tr_id = m.group(1)
    b = m.group(2)
    go_id = re.search(r"m_GameObject: \{fileID: (\d+)\}", b)
    father_id = re.search(r"m_Father: \{fileID: (\d+)\}", b)
    pos = re.search(r"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^\}]+)\}", b)
    transforms[tr_id] = {
        "go_id": go_id.group(1) if go_id else None,
        "father_id": father_id.group(1) if father_id else None,
        "pos": pos.groups() if pos else (0, 0, 0)
    }

# Map GameObject id to {name, transform_id}
go_info = {}
for m in re.finditer(r"--- !u!1 &(\d+)\nGameObject:(.*?)(?=\n---|\Z)", text, re.DOTALL):
    go_id = m.group(1)
    b = m.group(2)
    name = re.search(r"m_Name: ([^\n]+)", b)
    go_info[go_id] = name.group(1) if name else "Unnamed"

def get_path(tr_id):
    curr = tr_id
    parts = []
    while curr and curr in transforms:
        t = transforms[curr]
        g_id = t["go_id"]
        g_name = go_info.get(g_id, f"GO_{g_id}")
        parts.append(g_name)
        curr = t["father_id"]
    return " / ".join(reversed(parts))

for m in re.finditer(r"--- !u!108 &(\d+)\nLight:(.*?)(?=\n---|\Z)", text, re.DOTALL):
    light_id = m.group(1)
    block = m.group(2)
    go_id_m = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    go_id = go_id_m.group(1) if go_id_m else "None"
    go_name = go_info.get(go_id, "Unknown")
    
    # Find transform of this go_id
    tr_id = None
    for tid, tinfo in transforms.items():
        if tinfo["go_id"] == go_id:
            tr_id = tid
            break
            
    path = get_path(tr_id) if tr_id else "No Transform"
    lpos = transforms[tr_id]["pos"] if tr_id else "?"
    
    type_m = re.search(r"m_Type: (\d+)", block)
    ltype = type_m.group(1) if type_m else "?"
    typename = {"0": "Spot", "1": "Directional", "2": "Point", "3": "Area"}.get(ltype, ltype)
    range_m = re.search(r"m_Range: ([\d\.]+)", block)
    lrange = range_m.group(1) if range_m else "?"
    shadow_m = re.search(r"m_Shadows:\n    m_Type: (\d+)", block)
    shadow = shadow_m.group(1) if shadow_m else "?"
    shadowname = {"0": "None", "1": "Hard", "2": "Soft"}.get(shadow, shadow)
    intensity_m = re.search(r"m_Intensity: ([\d\.]+)", block)
    intensity = intensity_m.group(1) if intensity_m else "?"
    color_m = re.search(r"m_Color: \{r: ([\d\.]+), g: ([\d\.]+), b: ([\d\.]+)", block)
    color = color_m.groups() if color_m else "?"
    
    print("="*60)
    print(f"Name: {go_name} | Type: {typename} | Shadows: {shadowname}")
    print(f"Path: {path}")
    print(f"Pos: {lpos} | Range: {lrange} | Int: {intensity} | Color: {color}")
