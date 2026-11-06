"""Run the angular verification bench using only Python's standard library."""

import argparse
import csv
import hashlib
import json
import math
import platform
from dataclasses import asdict, replace
from pathlib import Path

from reference_model import Settings, rates, simulate, summarize, theoretical_rate_bound


def write_csv(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'results')
    args = ap.parse_args()
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    p = Settings()
    cases = [-150, -90, -45, 0, 45, 90, 150]
    summary, traces = [], []
    for ctrl in ['pfl3', 'proportional']:
        for e in cases:
            rows = simulate(e, settings=p, controller=ctrl)
            summary.append({'controller': ctrl, 'initial_error_deg': e, 'goal_deg': 0,
                            **summarize(rows, p)})
            traces += [{'controller': ctrl, 'initial_error_deg': e, **x} for x in rows]
    write_csv(out / 'pilot_summary.csv', summary)
    write_csv(out / 'pilot_traces.csv', traces)

    sweep = []
    for g in range(-180, 180, 10):
        for e in range(-170, 171, 10):
            l, r = rates(g - e, g)
            u = sum(r) - sum(l)
            sweep.append({'goal_deg': g, 'error_deg': e, 'right_minus_left': u,
                          'restoring_sign': (e == 0 or u * e > 0),
                          'max_rate': max(l + r)})
    write_csv(out / 'static_sweep.csv', sweep)

    rotated = []
    for g in range(-180, 180, 10):
        for e in cases:
            rows = simulate(e, goal_deg=g, settings=p)
            rotated.append({'goal_deg': g, 'initial_error_deg': e, **summarize(rows, p)})
    write_csv(out / 'rotated_goals.csv', rotated)

    edge = []
    for e in [-180, -179, -1, 0, 1, 179, 180]:
        for ctrl in ['pfl3', 'proportional']:
            rows = simulate(e, settings=p, controller=ctrl)
            edge.append({'controller': ctrl, 'initial_error_deg': e, **summarize(rows, p)})
    write_csv(out / 'edge_cases.csv', edge)

    sensitivity = []
    for dt in [0.01, 0.02, 0.04]:
        for gain in [0.5, 1.0, 2.0]:
            q = replace(p, dt_s=dt, turn_gain_deg_s_per_rate=gain)
            for e in [-150, -90, -45, 45, 90, 150]:
                rows = simulate(e, settings=q)
                sensitivity.append({'dt_s': dt, 'gain': gain, 'initial_error_deg': e,
                                    **summarize(rows, q)})
    write_csv(out / 'sensitivity.csv', sensitivity)

    pilot_pfl3 = [x for x in summary if x['controller'] == 'pfl3']
    checks = {
        'all_seven_pfl3_cases_settle': all(x['settling_time_s'] is not None for x in pilot_pfl3),
        'all_states_finite': all(x['finite'] for x in rotated + summary + edge + sensitivity),
        'yaw_limit_respected': all(x['max_abs_omega_deg_s'] <= p.omega_max_deg_s + 1e-12
                                   for x in rotated + summary + edge + sensitivity),
        'all_nonzero_static_signs_restore': all(x['restoring_sign'] for x in sweep),
        'theoretical_rate_bound_respected': all(x['max_rate'] <= theoretical_rate_bound() + 1e-10
                                                for x in sweep + rotated + summary + edge + sensitivity),
        'all_rotated_cases_settle': all(x['settling_time_s'] is not None for x in rotated),
        'all_sensitivity_cases_settle': all(x['settling_time_s'] is not None for x in sensitivity),
        'exact_antipodes_retained_as_failures': all(x['settling_time_s'] is None for x in edge
                             if x['controller'] == 'pfl3' and abs(x['initial_error_deg']) == 180),
    }
    report = {
        'scope': 'Angular bench of published PFL3 functional model; not a FlyWire-weighted neural simulation or Unity benchmark.',
        'settings': asdict(p), 'python_version': platform.python_version(),
        'pilot_pfl3_cases': len(pilot_pfl3), 'static_cases': len(sweep),
        'rotated_cases': len(rotated), 'sensitivity_cases': len(sensitivity),
        'rate_bound': theoretical_rate_bound(), 'checks': checks,
        'pilot_pfl3': pilot_pfl3,
        'rotated_max_final_error_deg': max(x['final_abs_error_deg'] for x in rotated),
        'source_code_sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
                 for p in sorted(Path(__file__).parent.glob('*.py'))
                 if p.name in ['reference_model.py', 'validate_reference.py']},
    }
    (out / 'validation_report.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(report, indent=2, ensure_ascii=False))
    if not all(checks.values()):
        raise SystemExit('A verification check failed; preserve and inspect the results.')


if __name__ == '__main__':
    main()
