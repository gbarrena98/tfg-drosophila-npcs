"""Audit a reproducible FlyWire subnetwork from the authors' public tables.

No neuron IDs are read as floats. Count and model sign are separate fields.
Annotations' `side` is NOT interpreted as PFL3 LAL output laterality.
"""

import argparse
import hashlib
import json
import platform
import urllib.request
from pathlib import Path

SOURCES = {
    'annotations_v2.0.0.tsv': {
        'url': 'https://raw.githubusercontent.com/flyconnectome/flywire_annotations/327ae7bdcc18c7a6a5ac014a4bfa9c7560fe854e/supplemental_files/Supplemental_file1_neuron_annotations.tsv',
        'sha256': '5cbd94da723111ad8512fca3aeb93eeb22dd9682d93995c7f6a23f05f68cb0eb',
    },
    'Connectivity_783.parquet': {
        'url': 'https://raw.githubusercontent.com/philshiu/Drosophila_brain_model/91bdd1e7dcf193f3e7ca5a8933497fcef63b7960/Connectivity_783.parquet',
        'sha256': 'efeb23fb99098e9c390f6869969b2a121a2ee92c833cfc45ecb2c1d8e1af0347',
    },
}
TYPES = ['EPG', 'Delta7', 'FC2A', 'FC2B', 'FC2C', 'PFL3']


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        while block := f.read(1024 * 1024):
            h.update(block)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--raw', type=Path, default=Path('raw'))
    ap.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'data')
    ap.add_argument('--download', action='store_true', help='Download the two pinned public tables (about 125 MB).')
    args = ap.parse_args()
    args.raw.mkdir(parents=True, exist_ok=True)
    args.output.mkdir(parents=True, exist_ok=True)
    for name, source in SOURCES.items():
        path = args.raw / name
        if not path.exists() and args.download:
            with urllib.request.urlopen(source['url'], timeout=180) as r, path.open('wb') as f:
                while block := r.read(1024 * 1024):
                    f.write(block)
        if not path.exists():
            raise SystemExit(f'Missing {path}; use --download or supply --raw.')
        if digest(path) != source['sha256']:
            raise SystemExit(f'SHA-256 mismatch for {name}; no output written.')

    import pandas as pd
    import numpy as np
    import pyarrow
    ann = pd.read_csv(args.raw / 'annotations_v2.0.0.tsv', sep='\t', dtype=str)
    cols = ['Presynaptic_ID', 'Postsynaptic_ID', 'Connectivity', 'Excitatory', 'Excitatory x Connectivity']
    con = pd.read_parquet(args.raw / 'Connectivity_783.parquet', columns=cols)
    if con[['Presynaptic_ID', 'Postsynaptic_ID']].duplicated().any():
        raise ValueError('Duplicate source neuron pairs require explicit aggregation.')
    selected = ann[ann['hemibrain_type'].isin(TYPES)].copy()
    if selected.root_id.duplicated().any():
        raise ValueError('Duplicate annotation IDs.')
    ids = set(selected.root_id.astype('int64'))
    all_ids = set(con.Presynaptic_ID) | set(con.Postsynaptic_ID)
    missing = sorted(ids - all_ids)
    if missing:
        raise ValueError(f'Selected IDs absent from connectivity: {missing}')
    edge = con[con.Presynaptic_ID.isin(ids) & con.Postsynaptic_ID.isin(ids)].copy()
    names = selected.set_index(selected.root_id.astype('int64')).hemibrain_type.to_dict()
    edge['pre_type'] = edge.Presynaptic_ID.map(names)
    edge['post_type'] = edge.Postsynaptic_ID.map(names)
    edge = edge.rename(columns={'Presynaptic_ID':'pre_root_id','Postsynaptic_ID':'post_root_id',
            'Connectivity':'synapse_count','Excitatory':'source_model_sign',
            'Excitatory x Connectivity':'source_signed_count'})
    edge = edge.sort_values(['pre_root_id','post_root_id'])
    selected = selected.sort_values('root_id')
    selected.to_csv(args.output / 'selected_neurons.csv', index=False)
    edge.to_csv(args.output / 'selected_edges.csv', index=False)
    totals = edge.groupby(['pre_type','post_type']).agg(pair_count=('synapse_count','size'),
                  synapse_count=('synapse_count','sum')).reset_index()
    totals.to_csv(args.output / 'connectivity_by_type.csv', index=False)
    matrix = totals.pivot(index='pre_type',columns='post_type',values='synapse_count').reindex(index=TYPES,columns=TYPES).fillna(0).astype(int)
    matrix.to_csv(args.output / 'type_synapse_matrix.csv')
    input_all = con[con.Postsynaptic_ID.isin(set(selected.loc[selected.hemibrain_type=='PFL3','root_id'].astype('int64')))]
    internal_pfl = int(edge.loc[edge.post_type=='PFL3','synapse_count'].sum())
    pfl_input = int(input_all.Connectivity.sum())
    report = {
        'dataset': 'FlyWire FAFB v783 via Shiu et al. public derived connectivity table',
        'annotation_release': 'v2.0.0', 'selection_column': 'hemibrain_type',
        'selection_types': TYPES, 'synapse_threshold': 1,
        'source_neurons_annotations': len(ann), 'source_directed_pairs': len(con),
        'source_synapse_count': int(con.Connectivity.sum()),
        'selected_neurons': len(selected), 'selected_directed_pairs': len(edge),
        'selected_synapse_count': int(edge.synapse_count.sum()),
        'neurons_by_type': {t:int((selected.hemibrain_type==t).sum()) for t in TYPES},
        'selected_ids_missing_from_connectivity': missing,
        'pfl3_total_input_synapses_source': pfl_input,
        'pfl3_input_synapses_from_selected_subnetwork': internal_pfl,
        'pfl3_input_fraction_from_selected_subnetwork': internal_pfl/pfl_input,
        'sources': SOURCES,
        'warnings': [
            'Model sign is supplied by the source simulation and is not a direct physiological measurement.',
            'Annotation side is not an audited LAL output label.',
            'This edge list is audited but is not used as the weight matrix of the functional reference model.',
            'The selected induced subgraph omits all other inputs and outputs; it is not a closed biological circuit.',
            'The source table does not provide synapse neuropil location or receptor-specific efficacy.'
        ],
        'environment': {'python':platform.python_version(), 'pandas':pd.__version__,
                        'numpy':np.__version__, 'pyarrow':pyarrow.__version__},
    }
    (args.output / 'extraction_report.json').write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps(report,indent=2,ensure_ascii=False))


if __name__ == '__main__':
    main()
