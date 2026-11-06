"""Plot development trajectories from the saved CSV files.
Optional dependencies: matplotlib and numpy. Does not affect the Unity project.
"""
from pathlib import Path
import csv
import json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.lines import Line2D
from matplotlib.patches import Circle

root = Path(__file__).resolve().parents[1]
out = root / 'Docs/Figures'
out.mkdir(parents=True, exist_ok=True)
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':12,'axes.titlesize':14,'axes.labelsize':12,
                     'xtick.labelsize':11,'ytick.labelsize':11,'svg.fonttype':'none'})
fig, axes = plt.subplots(1, 3, figsize=(10.4, 4.2))
colors = {'Proportional':'#AC6500', 'Pfl3':'#007E91'}
titles = ['A  Espacio abierto','B  Obstáculos','C  Cambio de meta']
for i, ax in enumerate(axes):
    folder = root / f'Validation/Executed/example_{i}'
    d = json.loads((folder / 'configuration.json').read_text())
    for obstacle in d['obstacles']:
        c = obstacle['center']
        ax.add_patch(Circle((c['x'], c['z']), obstacle['radius'], color='#77828A', alpha=.8))
        ax.add_patch(Circle((c['x'], c['z']), obstacle['radius']+.45, color='#77828A', fill=False, linestyle=':', linewidth=1))
    for kind in colors:
        with (folder / f'trace_{kind}.csv').open() as f:
            rows = list(csv.DictReader(f))
        ax.plot([float(r['x_m']) for r in rows], [float(r['z_m']) for r in rows],
                color=colors[kind], lw=2.4 if kind=='Proportional' else 1.8,
                ls='-' if kind=='Proportional' else '--', zorder=3)
    ax.plot(d['start']['x'], d['start']['z'], 'o', color='#263946', ms=6, zorder=4)
    ax.plot(d['goal']['x'], d['goal']['z'], '*', color='#3A803A', ms=13, zorder=4)
    if i == 2:
        ax.plot(d['secondGoal']['x'], d['secondGoal']['z'], '*', color='#3A803A', ms=13, zorder=4)
        ax.annotate('Meta 1', (d['goal']['x'],d['goal']['z']), xytext=(-29,11), textcoords='offset points', fontsize=10)
        ax.annotate('Meta 2', (d['secondGoal']['x'],d['secondGoal']['z']), xytext=(-44,11), textcoords='offset points', fontsize=10)
    ax.set(xlim=(-9,9), ylim=(-9,9), xlabel='X (m)', title=titles[i], xticks=[-8,0,8], yticks=[-8,0,8])
    ax.set_aspect('equal'); ax.grid(alpha=.18); ax.spines[['top','right']].set_visible(False)
    if i==0:ax.set_ylabel('Z (m)')
handles=[Line2D([0],[0],color=colors['Proportional'],lw=2.4,label='Proporcional'),
         Line2D([0],[0],color=colors['Pfl3'],lw=1.8,ls='--',label='PFL3'),
         Line2D([0],[0],color='#263946',marker='o',ls='',label='Inicio'),
         Line2D([0],[0],color='#3A803A',marker='*',ls='',ms=11,label='Meta')]
fig.legend(handles=handles,loc='lower center',ncol=4,frameon=False,bbox_to_anchor=(.5,.01),fontsize=12)
fig.subplots_adjust(left=.07,right=.985,top=.88,bottom=.25,wspace=.3)
for ext in ['png','svg']:
    fig.savefig(out / f'figura_1_trayectorias.{ext}',dpi=240,facecolor='white')
print(out / 'figura_1_trayectorias.png')
