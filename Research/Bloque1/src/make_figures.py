"""Generate exact figures from the saved extraction and verification results."""
import csv
import math
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.colors import LogNorm
from reference_model import rates

BASE = Path(__file__).resolve().parents[1]
OUT = BASE / 'figures'
OUT.mkdir(exist_ok=True)
plt.rcParams.update({'font.family':'DejaVu Sans', 'font.size':10,
                     'axes.spines.top':False,'axes.spines.right':False,
                     'svg.fonttype':'none'})

def save(fig,name):
    fig.savefig(OUT / f'{name}.png', dpi=240, bbox_inches='tight', facecolor='white')
    fig.savefig(OUT / f'{name}.svg', bbox_inches='tight', facecolor='white')
    plt.close(fig)

with (BASE/'data/type_synapse_matrix.csv').open() as f:
    data=list(csv.reader(f))
types=data[0][1:]
matrix=np.array([[int(v) for v in row[1:]] for row in data[1:]])
fig,ax=plt.subplots(figsize=(6.8,5.0))
im=ax.imshow(np.ma.masked_equal(matrix,0),norm=LogNorm(vmin=1,vmax=matrix.max()),cmap='Blues')
for i in range(6):
    for j in range(6):
        ax.text(j,i,str(matrix[i,j]),ha='center',va='center',
                color='white' if matrix[i,j]>500 else '#111111',fontsize=10)
ax.set(xticks=range(6),yticks=range(6),xticklabels=types,yticklabels=types,
       xlabel='Tipo postsináptico',ylabel='Tipo presináptico')
ax.spines[['top','right','bottom','left']].set_visible(False)
cb=fig.colorbar(im,ax=ax,fraction=.045,pad=.04)
cb.set_label('Sinapsis de la tabla fuente\n(escala de color logarítmica)')
fig.tight_layout()
save(fig,'figura_1_conectividad')

errors=np.linspace(-180,180,721)
left,right=[],[]
for error in errors:
    l,r=rates(-float(error),0.0)
    left.append(sum(l));right.append(sum(r))
fig,axs=plt.subplots(2,1,figsize=(6.8,4.8),sharex=True,gridspec_kw={'height_ratios':[1,1]})
axs[0].plot(errors,left,color='#215b9c',label='Suma izquierda')
axs[0].plot(errors,right,color='#aa431a',linestyle='--',label='Suma derecha')
axs[0].set_ylabel('Tasa agregada\ndel modelo')
axs[0].legend(frameon=False,loc='lower center',bbox_to_anchor=(0.5,1.01),ncol=2,fontsize=9)
axs[1].plot(errors,np.array(right)-np.array(left),color='#202020',label='Derecha menos izquierda')
axs[1].axhline(0,lw=.7,color='#999999')
axs[1].axvline(0,lw=.7,color='#999999')
axs[1].scatter([-180,180],[0,0],s=35,facecolors='white',edgecolors='#a52727',zorder=4)
axs[1].set(xlabel='Error angular G − H (grados)',ylabel='Señal de giro\nantes de la ganancia',xticks=[-180,-90,0,90,180],xlim=(-185,185))
fig.tight_layout()
save(fig,'figura_2_senal_giro')

with (BASE/'results/pilot_traces.csv').open() as f:
    rows=[r for r in csv.DictReader(f) if r['controller']=='pfl3']
fig,ax=plt.subplots(figsize=(6.8,4.0))
colors=['#8a2525','#bc6d16','#988211','#777777','#33856b','#236b9c','#62508a']
for e,col in zip([-150,-90,-45,0,45,90,150],colors):
    r=[r for r in rows if int(r['initial_error_deg'])==e and float(r['time_s'])<=4]
    ax.plot([float(x['time_s']) for x in r],[float(x['error_deg']) for x in r],
            label=f'{e:+d}°',color=col,linestyle='-' if e>=0 else '--',lw=1.5)
ax.axhspan(-2,2,facecolor='#dddddd',alpha=.6)
ax.set(xlabel='Tiempo simulado (s)',ylabel='Error angular (grados)',xlim=(0,4),ylim=(-160,160),
       yticks=[-150,-90,-45,0,45,90,150])
ax.legend(frameon=False,ncol=4,loc='upper right',fontsize=9,title='Error inicial')
ax.grid(axis='y',color='#eeeeee',lw=.6)
fig.tight_layout()
save(fig,'figura_3_validacion_angular')
print('Created three PNG figures and three editable SVG figures.')
