"""Reduced PFL3 population model and explicitly added control dynamics.

Reference: Mussells Pires et al. Nature 626, 808-818 (2024).
The arrays and fitted parameters follow MATLAB/ModelLAL.m, commit
0a9d5f643d2ed63e4e02f3e3ffba9d70e19e21a2 (MaimonLab, MIT).
This is NOT a dynamical simulation of the extracted FlyWire edge list.
The low-pass filter, gain and yaw limit are engineering additions for the TFG.
Angles are in degrees; positive yaw means rightward turning in this bench.
"""

from dataclasses import dataclass
import math


RIGHT_PREF = tuple(-x for x in (67.5, 112.5, 157.5, 157.5, -157.5,
                              -112.5, -112.5, -67.5, -22.5, -22.5, 22.5, 67.5))
LEFT_PREF = tuple(-x for x in (-67.5, -22.5, 22.5, 22.5, 67.5,
                             112.5, 112.5, 157.5, -157.5, -157.5, -112.5, -67.5))
GOAL_PREF = tuple(-(15 + 30 * i) for i in range(12))
A, B, C, D = 29.2282, 2.1736, -0.7011, 0.6299


def wrap_deg(angle):
    """Shortest signed circular difference, including a signed antipodal tie."""
    rad = math.radians(angle)
    return math.degrees(math.atan2(math.sin(rad), math.cos(rad)))


def softplus(x):
    return max(x, 0.0) + math.log1p(math.exp(-abs(x)))


def rates(heading_deg, goal_deg):
    """Return twelve left and twelve right nonnegative target rates."""
    g = [D * math.cos(math.radians(goal_deg - p)) for p in GOAL_PREF]
    left = [A * softplus(B * (math.cos(math.radians(heading_deg - p)) + q + C))
            for p, q in zip(LEFT_PREF, g)]
    right = [A * softplus(B * (math.cos(math.radians(heading_deg - p)) + q + C))
             for p, q in zip(RIGHT_PREF, g)]
    return left, right


def theoretical_rate_bound():
    return A * softplus(B * (1.0 + D + C))


@dataclass(frozen=True)
class Settings:
    dt_s: float = 0.02
    horizon_s: float = 10.0
    tau_s: float = 0.10
    turn_gain_deg_s_per_rate: float = 1.0
    omega_max_deg_s: float = 120.0
    proportional_gain_s_inv: float = 2.0
    settle_deg: float = 2.0
    dwell_s: float = 0.5


class PFL3Controller:
    def __init__(self, settings):
        self.settings = settings
        self.state = [0.0] * 24

    def step(self, heading_deg, goal_deg):
        left, right = rates(heading_deg, goal_deg)
        target = left + right
        p = self.settings
        alpha = 1.0 if p.tau_s == 0 else -math.expm1(-p.dt_s / p.tau_s)
        self.state = [old + alpha * (new - old)
                      for old, new in zip(self.state, target)]
        raw = sum(self.state[12:]) - sum(self.state[:12])
        yaw = p.turn_gain_deg_s_per_rate * raw
        return max(-p.omega_max_deg_s, min(p.omega_max_deg_s, yaw))


def simulate(initial_error_deg, goal_deg=0.0, settings=Settings(), controller='pfl3'):
    """Stationary-goal orientation bench; no position, obstacles or Unity physics."""
    heading = wrap_deg(goal_deg - initial_error_deg)
    model = PFL3Controller(settings)
    rows = []
    for k in range(round(settings.horizon_s / settings.dt_s) + 1):
        t = k * settings.dt_s
        error = wrap_deg(goal_deg - heading)
        if k == round(settings.horizon_s / settings.dt_s):
            omega = 0.0
        elif controller == 'pfl3':
            omega = model.step(heading, goal_deg)
        elif controller == 'proportional':
            omega = max(-settings.omega_max_deg_s,
                        min(settings.omega_max_deg_s,
                            settings.proportional_gain_s_inv * error))
        else:
            raise ValueError(controller)
        rows.append({'time_s': t, 'heading_deg': heading, 'goal_deg': goal_deg,
                     'error_deg': error, 'omega_deg_s': omega,
                     'max_rate': max(model.state)})
        if k < round(settings.horizon_s / settings.dt_s):
            heading = wrap_deg(heading + settings.dt_s * omega)
    return rows


def summarize(rows, settings):
    errors = [abs(x['error_deg']) for x in rows]
    # Left-endpoint Riemann average over [0,T); no extra weight for the endpoint.
    mean_abs = sum(errors[:-1]) * settings.dt_s / settings.horizon_s
    # First entry into tolerance that remains there through the end of the bench.
    # Require at least dwell_s observed in-band after this entry.
    last_bad = max((i for i, e in enumerate(errors) if e >= settings.settle_deg),
                   default=-1)
    first_good = last_bad + 1
    settle = None
    if first_good < len(rows):
        entry = rows[first_good]['time_s']
        if rows[-1]['time_s'] - entry + 1e-12 >= settings.dwell_s:
            settle = entry
    return {'final_abs_error_deg': errors[-1], 'mean_abs_error_deg': mean_abs,
            'settling_time_s': settle,
            'max_abs_omega_deg_s': max(abs(x['omega_deg_s']) for x in rows),
            'max_rate': max(x['max_rate'] for x in rows),
            'finite': all(math.isfinite(v) for x in rows for v in x.values())}
