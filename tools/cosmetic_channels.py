"""Recolour channels of costume materials.

The game gives every cosmetic slot one colour. The mod adds two more per slot (CostumeChannels.cs), so a part's
materials are split into up to three channels: 0 = main (the game's own slot colour), 1 = second, 2 = accent.
Tintable fabrics come first; channels left over go to the larger fixed fabrics, which keep their own colour until picked.
Every exporter calls assign() on a part's material list; run this file to (re)mark the models already in src/Art:

    python tools/cosmetic_channels.py
"""
import json
import pathlib
import re

MAX_CHANNELS = 3

# How "secondary" a material is, by a word in its name (first match wins, so the specific ones come first).
# Anything else is the part's main colour. Materials of equal rank keep the order they appear in.
RANKS = (('gem', 9), ('accent', 8), ('trim', 7), ('wing', 6), ('stripe', 6), ('red leather', 6), ('lace', 5),
         ('dark trousers', 4), ('ivory linen', 3))


def base_name(name):
    """'Shark skin shark_tail 3' -> 'shark skin': the same fabric used twice in a part is one channel."""
    return re.sub(r' [a-z_]+ \d+$', '', name).lower()


def rank(name):
    name = base_name(name)
    for word, value in RANKS:
        if word in name: return value
    return 0


# Fixed details that never get a channel of their own: bodies, faces and glowing bits.
NEVER = ('skin', 'eye', 'glint', 'tooth', 'teeth', 'gum', 'lips', 'brow', 'lash', 'bone', 'rib', 'glow', 'crystal', 'amber', 'gem')
MIN_SHARE = .08   # a fixed fabric needs this much of the part's surface to be worth a channel


def optional_ok(material):
    """A fixed material that can be an optional channel: its texture is grey shading over the material colour."""
    name = base_name(material['name'])
    return not material.get('tint') and (not material.get('texture') or material.get('multiply')) and not any(w in name for w in NEVER)


def assign(materials, parts=None):
    """Set 'channel' on every material of one part.

    Tintable fabrics get 0, 1, 2 in rank order (0 is the slot colour). Channels left over go to the largest fixed
    fabrics (hair, lining, leather, metal): those keep 'tint' false and their own colour until the player picks one,
    and never use channel 0. Without part data (triangle counts) only the tintable fabrics are marked."""
    groups = []
    for m in materials:
        if m.get('tint') and base_name(m['name']) not in groups: groups.append(base_name(m['name']))
    groups.sort(key=rank)   # stable: ties keep their order of appearance
    area = {}
    total = 0
    for part in parts or []:
        name = base_name(materials[part['material']]['name'])
        area[name] = area.get(name, 0) + len(part['triangles'])
        total += len(part['triangles'])
    extra = []
    for m in materials:
        name = base_name(m['name'])
        if optional_ok(m) and name not in extra and name not in groups and total and area.get(name, 0) >= MIN_SHARE * total: extra.append(name)
    extra.sort(key=lambda name: -area[name])
    first = max(min(len(groups), MAX_CHANNELS), 1)   # channel 0 is always the slot colour
    extra = extra[:MAX_CHANNELS - first]
    for m in materials:
        name = base_name(m['name'])
        if m.get('tint'): m['channel'] = min(groups.index(name), MAX_CHANNELS - 1)
        elif name in extra: m['channel'] = first + extra.index(name)
        else: m['channel'] = 0
    return materials


def channels(materials):
    """Channel -> fabric names, for the tintable (always coloured) and the optional fabrics of a part."""
    found = {}
    for m in materials:
        if m.get('tint') or m.get('channel', 0) > 0:
            found.setdefault(m.get('channel', 0), []).append(base_name(m['name']) + ('' if m.get('tint') else '*'))
    return {c: list(dict.fromkeys(names)) for c, names in sorted(found.items())}


def patch(path):
    """Mark one exported model in place, touching only its material list."""
    text = path.read_text(encoding='utf-8')
    start = text.index('"materials":[') + len('"materials":')
    end = text.index(']', start) + 1   # materials hold no arrays, so the first ']' closes the list
    model = json.loads(text)
    before = json.dumps(json.loads(text[start:end]), separators=(',', ':'))
    after = json.dumps(assign(model['materials'], model['parts']), separators=(',', ':'))
    if before != after: path.write_text(text[:start] + after + text[end:], encoding='utf-8', newline='')
    return model['materials'], before != after


def cosmetic_keys(root):
    wings = (root / 'src/Cosmetics/WingCosmetics.cs').read_text(encoding='utf-8')
    catalogue = (root / 'src/Cosmetics/MascotCosmetics.cs').read_text(encoding='utf-8')
    keys = re.findall(r'new\("([a-z_]+)",', wings)
    sets = re.findall(r'\("([a-z]+)", "[^"]+"\)', catalogue)
    pieces = re.findall(r'Part\(set, "([a-z_]+)"', catalogue)
    keys += [s + '_' + p for s in sets for p in pieces]
    keys += re.findall(r'new WingCosmetics\.Definition\("([a-z_]+)"', catalogue)
    return keys


if __name__ == '__main__':
    root = pathlib.Path(__file__).resolve().parent.parent
    changed = 0; multi = 0; none = []
    for key in cosmetic_keys(root):
        path = root / 'src/Art' / f'v11_{key}.mesh.json'
        if not path.exists(): continue   # only some sets have a face and a jaw
        materials, touched = patch(path)
        changed += touched
        found = channels(materials)
        multi += len(found) > 1
        if not found: none.append(key)
        print(f'{key:24s} ' + ' | '.join(', '.join(names) for names in found.values()))
    print(f'{changed} model(s) updated, {multi} part(s) with more than one channel (* = keeps its own colour until picked)')
    print('no channels: ' + ', '.join(none))
