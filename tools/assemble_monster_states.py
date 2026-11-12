"""Assemble the local model-state renders into readable contact sheets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[1]
folder=root/'art'/'monsters'/'states'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
sets={
 'witch':[('idle','Покой'),('fire_prep','Подготовка огненного шара'),('electric','Электрический каст')],
 'basilisk':[('crawl','Ползёт'),('warning','Предупреждение перед броском'),('constrict','Обвивание')],
 'gorgon':[('idle','Патруль'),('charge','Таран'),('stare','Смертельный взгляд')],
 'naga':[('idle','Покой'),('combo','Удар мечом'),('gaze','Окаменяющий взгляд')],
 'pig':[('flying','Полёт'),('eating','Кормление'),('landed','После приземления')],
}
for key,states in sets.items():
    sheet=Image.new('RGB',(1800,660),(32,34,39)); draw=ImageDraw.Draw(sheet)
    for i,(state,label) in enumerate(states):
        picture=Image.open(folder/(key+'_'+state+'.png')).convert('RGB').resize((600,600),Image.Resampling.LANCZOS)
        sheet.paste(picture,(i*600,60))
        draw.text((i*600+300,30),label,font=font,fill=(239,234,225),anchor='mm')
    sheet.save(folder/(key+'_sheet.png'))
# Reactions shared by all seven monsters: hurt flinch, struggling in a player's grip, stunned.
for key in ('pig','witch','basilisk','gorgon','naga','fairy','twins'):
    if not all((folder/(key+'_'+state+'.png')).exists() for state in ('hurt','held','stunned')): continue
    sheet=Image.new('RGB',(1800,660),(32,34,39)); draw=ImageDraw.Draw(sheet)
    for i,(state,label) in enumerate([('hurt','Получение урона'),('held','В захвате у игрока'),('stunned','Оглушение')]):
        picture=Image.open(folder/(key+'_'+state+'.png')).convert('RGB').resize((600,600),Image.Resampling.LANCZOS)
        sheet.paste(picture,(i*600,60))
        draw.text((i*600+300,30),label,font=font,fill=(239,234,225),anchor='mm')
    sheet.save(folder/(key+'_reactions_sheet.png'))
sheet=Image.new('RGB',(1200,660),(32,34,39));draw=ImageDraw.Draw(sheet)
for i,(key,label) in enumerate([('fairy','Шкодная фея — концепт'),('twins','Гензель и Греттель — концепт')]):
    sheet.paste(Image.open(root/'art'/'monsters'/(key+'.png')).convert('RGB').resize((600,600),Image.Resampling.LANCZOS),(600*i,60))
    draw.text((600*i+300,30),label,font=font,fill=(239,234,225),anchor='mm')
sheet.save(folder/'concepts_sheet.png')
print('Contact sheets:',folder)
