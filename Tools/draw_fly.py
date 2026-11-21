"""Draw the original dorsal fly illustration used on the title screen (Pillow)."""
from pathlib import Path
from math import cos, sin
from PIL import Image, ImageDraw

SIZE = 3
W, H = 440, 475
image = Image.new('RGBA', (W * SIZE, H * SIZE))
draw = ImageDraw.Draw(image)
CX = W / 2


def coords(points, side=1):
    return [(round((CX + side * x) * SIZE), round(y * SIZE)) for x, y in points]


def curve(start, *segments):
    points = [start]
    for control1, control2, end in segments:
        p = points[-1]
        for j in range(1, 21):
            t = j / 20
            u = 1 - t
            points.append(tuple(u**3*p[i] + 3*u*u*t*control1[i] + 3*u*t*t*control2[i] + t**3*end[i] for i in (0, 1)))
    return points


def line(points, color, width=1, side=1):
    draw.line(coords(points, side), fill=color, width=max(1, round(width * SIZE)), joint='curve')


def shape(points, fill, outline, side=1, width=1):
    draw.polygon(coords(points, side), fill=fill)
    if outline:
        line(points + [points[0]], outline, width, side)


def oval(x, y, rx, ry, fill, outline=None, width=1):
    draw.ellipse(((CX + x-rx)*SIZE, (y-ry)*SIZE, (CX+x+rx)*SIZE, (y+ry)*SIZE),
                 fill=fill, outline=outline, width=max(1, round(width*SIZE)))


def bilateral(part):
    return part + [(-x, y) for x, y in reversed(part)]


def draw_legs(side):
    legs = [[(21, 151),(54, 133),(91, 100),(108, 57),(118, 47)],
            [(26, 177),(60, 176),(99, 201),(150, 180),(174, 161)],
            [(27, 196),(71, 232),(100, 282),(150, 305),(168, 313)]]
    for leg in legs:
        for i, (start, end) in enumerate(zip(leg, leg[1:])):
            line([start, end], '#73533c', 5-i*.75, side)
            line([start, end], '#c99a62', 1.5-i*.18, side)
            if i in (1, 2):
                for j in range(2, 7):
                    t=j/8
                    x=start[0]+(end[0]-start[0])*t
                    y=start[1]+(end[1]-start[1])*t
                    line([(x,y),(x+4+(j%2),y+4)], '#a98962', .7, side)
        x,y=leg[-1]
        line([(x,y),(x+4,y-4),(x+4.5,y-1.5)], '#c3ab86', 1, side)


for side in (-1, 1):
    draw_legs(side)
    oval(side * 45, 229, 5, 4, '#d8b47e')
    line([(35,220),(45,229)], '#cda26e', 1.6, side)

# The tergites form five overlapping bands narrowing towards the abdomen tip.
abdomen = curve((0,182), ((32,182),(49,220),(46,271)), ((45,310),(28,362),(0,385)))
shape(bilateral(abdomen), '#9e6136', '#d4a267', width=1.5)
for y, half, bottom_half, depth in [(216,40,45,26),(244,45,45,26),(273,45,40,25),(301,39,31,22),(328,30,21,16),(349,21,9,15)]:
    band=curve((-half,y), ((-half*.47,y+8),(half*.45,y+8),(half,y)),
               ((half,y+6),(bottom_half,y+depth-3),(bottom_half,y+depth)),
               ((half*.38,y+depth+12),(-half*.38,y+depth+12),(-bottom_half,y+depth)),
               ((-bottom_half,y+depth-1),(-half,y+6),(-half,y)))
    shape(band,'#412d29','#956139',width=.65)
    line(curve((-half+3,y+1), ((-half*.46,y+9),(half*.43,y+9),(half-3,y+1))), '#e1a55b', 1.6)
for side in (-1,1):
    line(curve((7,207),((18,254),(17,320),(2,365))), '#c98a53', 1.1, side)
    for j in range(14):
        y=224+j*9
        x=15+4*sin(j*.3) if j<10 else 13-j*.53
        line([(x,y),(x+2.2,y+4)], '#d8a36d', .8, side)

thorax = curve((0,105),((32,101),(51,129),(49,168)),((48,194),(29,216),(0,224)))
shape(bilateral(thorax), '#bd8857', '#edc392', width=1.8)
for side in (-1,1):
    shape(curve((7,114),((25,117),(33,140),(29,167)),((29,191),(20,204),(9,207)),((17,183),(18,138),(7,114))),
          '#745140', '#a07350',side)
    line(curve((5,112),((17,134),(16,175),(5,209))), '#e0ab72', 2.6, side)
    line(curve((29,117),((48,145),(39,177),(29,188))), '#efc596', 1.4,side)
    for j in range(11):
        y=123+j*6
        x=16+8*sin(j*.3)
        line([(x,y),(x+3.7,y+6.4)],'#e7ba86',.9,side)
    for j in range(5):
        line([(32+j,141+j*9),(48+j,143+j*10)],'#976c4e',1.2,side)

# A translucent membrane lets the body and legs remain visible through each wing.
for side in (-1,1):
    layer = Image.new('RGBA',image.size)
    ld=ImageDraw.Draw(layer)
    wing=curve((34,151),((60,148),(87,185),(117,250)),((140,298),(172,363),(179,410)),
               ((183,430),(166,438),(151,422)),((104,372),(56,292),(39,231)),((28,191),(29,164),(34,151)))
    ld.polygon(coords(wing,side),fill=(124,180,170,84))
    image.alpha_composite(layer)
    line(wing+[wing[0]], '#9bb5a7',1.7,side)
    leading=curve((36,154),((67,162),(86,199),(114,252)),((138,302),(166,366),(177,415)))
    line(leading,'#d1b890',2.5,side)
    veins=[curve((36,156),((57,185),(84,237),(101,272)),((124,316),(145,365),(160,418))),
           curve((36,160),((53,211),(73,264),(94,310)),((111,347),(133,391),(150,420))),
           curve((37,167),((42,214),(51,248),(74,294)),((89,322),(104,352),(119,374))),
           curve((39,179),((45,231),(56,269),(69,286))),
           curve((66,213),((75,212),(80,210),(85,209))),
           curve((82,285),((89,280),(93,275),(101,272))),
           curve((129,352),((135,349),(144,343),(150,344)))]
    for j,v in enumerate(veins):
        line(v,'#a4baa8' if j<3 else '#81a69c',1.45 if j<3 else 1.0,side)
    for j in range(7):
        v=curve((71+j*4,229+j*8),((75+j*4,231+j*8),(80+j*4,233+j*8),(83+j*4,236+j*8)))
        line(v,'#7c9b91',.55,side)

head=curve((0,67),((23,62),(41,72),(41,92)),((37,108),(16,116),(0,114)))
shape(bilateral(head),'#b99366','#e4c6a0',width=1.6)
for side in (-1,1):
    eye=curve((16,71),((24,64),(38,69),(42,82)),((46,95),(36,108),(26,108)),((12,107),(8,81),(16,71)))
    shape(eye,'#b95136','#e48c62',side,1.7)
    for row in range(7):
        for col in range(5):
            x=15+col*5.0+(row%2)*2.5
            y=73+row*4.6
            if ((x-27.5)/14.2)**2+((y-89)/18.1)**2<.84:
                oval(side*x,y,1.25,1.05,'#e47d4d' if row<4 else '#d16a45')
    line(curve((20,73),((14,80),(14,93),(18,99))), '#f4b786',1.1,side)
    oval(side*8,69,3.6,7,'#c39a65','#efd0a3')
    line(curve((9,64),((17,54),(24,48),(30,39))), '#d3ba92',1.35,side)
    for j in range(4):
        x=14+j*3.8
        y=59-j*5
        line([(x,y),(x+7,y-1)],'#c3ab85',.75,side)
        line([(x,y),(x+1,y-5)],'#c3ab85',.75,side)
    line(curve((3,85),((6,92),(6,98),(4,103))), '#efd1a3',.9,side)
for x,y in [(-3,91),(3,91),(0,86)]:
    oval(x,y,1.3,1.2,'#463532')

out=Path(__file__).resolve().parents[1]/'Assets/StreamingAssets/Drosophila.png'
out.parent.mkdir(parents=True,exist_ok=True)
image.resize((W,H),Image.Resampling.LANCZOS).save(out,optimize=True)
