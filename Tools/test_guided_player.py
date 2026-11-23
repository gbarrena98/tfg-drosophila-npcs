"""Drive the distributed Windows Player through its normal UI and check its exported trials."""
import csv
import ctypes
from ctypes import wintypes
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import zipfile

from PIL import ImageChops, ImageGrab

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()
user32.GetForegroundWindow.restype = wintypes.HWND
user32.EnumWindows.argtypes = [ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM), wintypes.LPARAM]
WINDOWPROC = user32.EnumWindows.argtypes[0]
ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'Builds/guide-checks'
TRIALS = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/German Barrena Alonso/Drosophila Navigation Lab/Trials'


def wait_for(read, message, timeout=45):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        result = read()
        if result:
            return result
        time.sleep(.25)
    raise RuntimeError(message)


def player_window(pid):
    found = []
    @WINDOWPROC
    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        rect = wintypes.RECT()
        user32.GetClientRect(hwnd, ctypes.byref(rect))
        if owner.value == pid and user32.IsWindowVisible(hwnd) and rect.right >= 600 and rect.bottom >= 360:
            found.append(hwnd)
        return True
    user32.EnumWindows(visit, 0)
    return found[0] if found else None


class GameWindow:
    def __init__(self, hwnd):
        self.hwnd = hwnd
        user32.ShowWindow(hwnd, 5)
        user32.SetForegroundWindow(hwnd)
        rect = wintypes.RECT()
        user32.GetClientRect(hwnd, ctypes.byref(rect))
        pt = wintypes.POINT()
        user32.ClientToScreen(hwnd, ctypes.byref(pt))
        self.left, self.top = pt.x, pt.y
        self.width, self.height = rect.right, rect.bottom
        self.scale = min(self.width / 1440, self.height / 900)
        self.dx = (self.width - 1440 * self.scale) / 2
        self.dy = (self.height - 900 * self.scale) / 2
        print('Player client', self.left, self.top, self.width, self.height, flush=True)

    def local_point(self, x, y):
        return round(self.dx + x * self.scale), round(self.dy + y * self.scale)

    def click(self, x, y):
        user32.SetForegroundWindow(self.hwnd)
        px, py = self.local_point(x, y)
        user32.SetCursorPos(self.left + px, self.top + py)
        time.sleep(.18)
        user32.mouse_event(2, 0, 0, 0, 0)
        time.sleep(.12)
        user32.mouse_event(4, 0, 0, 0, 0)
        time.sleep(.25)

    def drag(self, start, end):
        a,b=self.local_point(*start),self.local_point(*end)
        user32.SetCursorPos(self.left+a[0],self.top+a[1])
        user32.mouse_event(2,0,0,0,0)
        for i in range(1,9):
            time.sleep(.06)
            user32.SetCursorPos(self.left+round(a[0]+(b[0]-a[0])*i/8),self.top+round(a[1]+(b[1]-a[1])*i/8))
        time.sleep(.15)
        user32.mouse_event(4,0,0,0,0)
        time.sleep(.35)

    def set_angle(self, text, workshop=False):
        self.click(235,536 if workshop else 620)
        def key(code, released=False):
            user32.keybd_event(code,user32.MapVirtualKeyW(code,0),2 if released else 0,0)
            time.sleep(.065)
        key(0x11)
        key(ord('A'))
        key(ord('A'),True)
        key(0x11,True)
        for char in text:
            code={'-':0xBD,',':0xBC,'.':0xBE}.get(char,ord(char))
            key(code)
            key(code,True)

    def resize(self,width,height):
        rect=wintypes.RECT()
        user32.GetWindowRect(self.hwnd,ctypes.byref(rect))
        user32.SetWindowPos(self.hwnd,0,0,0,width+rect.right-rect.left-self.width,height+rect.bottom-rect.top-self.height,0x16)
        time.sleep(.8)
        self.__init__(self.hwnd)

    def card_borders(self):
        frame=self.capture()
        for x in (140,345,625,810,1020,1290):
            for y in (711,846):
                px,py=self.local_point(x,y)
                area=frame.crop((px-1,py-2,px+2,py+3)).convert('RGB')
                assert any(g>=70 and b>=73 and g-r>=18 for r,g,b in area.getdata()), ('Missing horizontal edge',x,y,self.width,self.height)
        for x in (448,892,1336):
            assert self.primary_visible(x,730), ('Circuit header missing',x,self.width,self.height)

    def capture(self):
        return ImageGrab.grab(bbox=(self.left, self.top, self.left + self.width, self.top + self.height), all_screens=True)

    def primary_visible(self, x, y):
        red, green, blue = self.capture().getpixel(self.local_point(x, y))[:3]
        return green > 105 and blue > 105 and green - red > 42

    def heading_arrow_visible(self, heading):
        frame = self.capture()
        radians = math.radians(heading)
        pixels_per_metre = 612 / (2 * 10.65)
        hits = 0
        for distance in (0.85, 1.05, 1.25, 1.45, 1.65, 1.85):
            world_x = -6 + distance * math.sin(radians)
            world_z = -6 + distance * math.cos(radians)
            x, y = self.local_point(696 + world_x * pixels_per_metre, 418 - world_z * pixels_per_metre)
            neighbourhood = frame.crop((x - 2, y - 2, x + 3, y + 3)).convert('RGB')
            if any(min(rgb) > 100 and max(rgb) - min(rgb) < 22 for rgb in neighbourhood.getdata()):
                hits += 1
        return hits >= 5

    def crop(self, left, top, right, bottom):
        return self.capture().crop((*self.local_point(left, top), *self.local_point(right, bottom))).convert('RGB')

    def goal_is_green(self, x, z):
        pixels_per_metre = 612 / (2 * 10.65)
        cx, cy = 696 + x * pixels_per_metre, 418 - z * pixels_per_metre
        area = self.crop(cx-17, cy-17, cx+17, cy+17)
        return sum(g > 105 and g-r > 24 and g-b > 26 for r,g,b in area.getdata()) >= 3

    def save(self, name):
        self.capture().save(EVIDENCE / (name + '.png'))


def session_matches(used, heading, mode, case):
    if not TRIALS.is_dir():
        return None
    for folder in sorted(TRIALS.iterdir(), reverse=True):
        if folder in used or not (folder / 'summary.csv').is_file():
            continue
        try:
            config = json.loads((folder / 'configuration.json').read_bytes())
            session = json.loads((folder / 'session.json').read_bytes())
            with (folder / 'summary.csv').open(encoding='utf-8-sig', newline='') as src:
                rows = {r['controller']: r for r in csv.DictReader(src)}
        except (OSError, ValueError, KeyError):
            continue
        if (len(rows) == 2 and (config['configurationId'] == case or (case.endswith('*') and config['configurationId'].startswith(case[:-1]))) and
                (heading is None or math.isclose(config['initialHeading'], heading, abs_tol=1e-8)) and
                'mode ' + mode in session['environment']):
            used.add(folder)
            return folder, config, rows
    return None


def csv_rows(path):
    with path.open(encoding='utf-8-sig', newline='') as source:
        return list(csv.DictReader(source))


def check_other_scenarios(window, used):
    window.click(170, 789)
    window.click(711, 768)
    window.click(235, 291)
    window.save('08-obstaculos-salida')
    window.click(160, 674)
    time.sleep(2)
    window.click(135, 577)
    window.click(1264, 649)
    only_reference = window.crop(341, 114, 1050, 722)
    window.click(1353, 649)
    only_pfl3 = window.crop(341, 114, 1050, 722)
    assert ImageChops.difference(only_reference, only_pfl3).getbbox(), 'Filtering did not change the arena.'
    window.save('09-obstaculos-aislados')
    window.click(1128, 649)
    window.click(135, 577)
    folder, case, obstacles = wait_for(lambda: session_matches(used, None, 'guided', 'B-00'), 'B-00 did not finish.')
    assert len(case['obstacles']) == 3
    for kind, row in obstacles.items():
        assert row['state'] == 'Success' and int(row['collisions']) == 0, row
        samples = csv_rows(folder / f'trace_{kind}.csv')
        assert any(abs(float(s['error_reference_deg'])-float(s['error_goal_deg'])) > 1 for s in samples)
        for obstacle in case['obstacles']:
            assert all(math.hypot(float(s['x_m'])-obstacle['center']['x'],float(s['z_m'])-obstacle['center']['z'])
                       >= obstacle['radius']+case['settings']['agentRadius']-1e-5 for s in samples)
    window.save('10-obstaculos-resultados')
    window.click(170, 619)
    _, _, repeat = wait_for(lambda: session_matches(used, None, 'guided', 'B-00'), 'B repeat did not finish.')
    for kind, row in repeat.items():
        assert (row['arrival_s'],row['route_m'],row['collisions']) == tuple(obstacles[kind][k] for k in ('arrival_s','route_m','collisions'))

    window.click(170, 789)
    window.click(1039, 770)
    window.click(235, 291)
    wait_for(lambda: window.goal_is_green(0,3) and not window.goal_is_green(6,6), 'Expected first active goal in C.', 10)
    window.save('11-cambio-salida')
    window.click(160, 674)
    time.sleep(6.8)
    window.save('12-espera')
    wait_for(lambda: window.goal_is_green(6,6) and not window.goal_is_green(0,3), 'Active marker did not move to goal 2.', 20)
    window.save('13-nueva-meta')
    folder, case, changed_goal = wait_for(lambda: session_matches(used, 0, 'guided', 'C-00'), 'C-00 did not finish.')
    for kind, row in changed_goal.items():
        switches = [float(ev['time_s']) for ev in csv_rows(folder / f'events_{kind}.csv') if ev['event'] == 'goal_switch']
        assert switches == [10] and row['state'] == 'Success' and float(row['arrival_s']) > 10 and row['reorientation_s']
        samples = csv_rows(folder / f'trace_{kind}.csv')
        assert any(s['waiting_for_switch']=='true' and float(s['time_s'])<10 for s in samples)
        for sample in samples:
            goal = case['secondGoal'] if float(sample['time_s']) >= 10 else case['goal']
            assert (float(sample['goal_x_m']),float(sample['goal_z_m'])) == (goal['x'],goal['z'])
    window.save('14-cambio-resultados')
    return obstacles, changed_goal


def verify_relay_session(folder, case, rows, evidence_name):
    """Check visits from the positions at handoff and recalculate responses from actual switches."""
    assert case['relayRule'] == 'all_participants_reached_after_minimum_time'
    responses = {(r['controller'], int(r['goal_number'])): r for r in csv_rows(folder/'goal_responses.csv')}
    assert len(responses) == 4
    handoffs = []
    evidence = {}
    for kind, row in rows.items():
        assert row['state'] == 'Success'
        events = csv_rows(folder/f'events_{kind}.csv')
        visits = [e for e in events if e['event'].startswith('beacon_')]
        assert [e['event'] for e in visits] == ['beacon_1_reached', 'beacon_2_reached', 'beacon_3_reached']
        reached = [float(e['time_s']) for e in visits]
        switches = [float(e['time_s']) for e in events if e['event'].startswith('goal_switch')]
        assert len(switches) == 2 and reached[0] <= switches[0] <= reached[1] <= switches[1] <= reached[2]
        assert reached[2] == float(row['arrival_s'])
        handoffs.append(switches)
        trace = csv_rows(folder/f'trace_{kind}.csv')
        radii = []
        latency = []
        for i, switch in enumerate(switches):
            sample = next(s for s in trace if math.isclose(float(s['time_s']), switch, abs_tol=1e-8))
            goal = case[('goal', 'secondGoal')[i]]
            distance = math.hypot(float(sample['x_m'])-goal['x'], float(sample['z_m'])-goal['z'])
            assert distance <= case['settings']['arrivalRadius']+1e-7, (kind, i, distance)
            radii.append(distance)
            response = responses[kind, i+2]
            assert math.isclose(float(response['switch_s']), switch, abs_tol=1e-8)
            end = switches[1] if i == 0 else reached[2]
            since = None
            expected = None
            for sample in trace:
                t = float(sample['time_s'])
                if not switch <= t < end:
                    continue
                if sample['waiting_for_switch'] == 'true' or abs(float(sample['error_reference_deg'])) >= case['settings']['reorientDegrees']:
                    since = None
                    continue
                if since is None:
                    since = t
                if t + float(sample['dt_s']) - since + 1e-9 >= case['settings']['reorientDwell']:
                    expected = since - switch
                    break
            reported = float(response['reorientation_s']) if response['reorientation_s'] else None
            assert (expected is None and reported is None) or (expected is not None and reported is not None and math.isclose(reported,expected,abs_tol=1e-8))
            latency.append(reported)
        for sample in trace:
            t = float(sample['time_s'])
            key = 'goal' if t < switches[0] else 'secondGoal' if t < switches[1] else 'thirdGoal'
            goal = case[key]
            assert (float(sample['goal_x_m']), float(sample['goal_z_m'])) == (goal['x'], goal['z'])
        evidence[kind] = {'reached_s': reached, 'switch_s': switches, 'distance_at_switch_m': radii, 'reorientation_s': latency, 'route_m': float(row['route_m'])}
    assert handoffs[0] == handoffs[1]
    for i, field in enumerate(('goalChangeSeconds', 'secondChangeSeconds')):
        earliest = max(case[field], *(proof['reached_s'][i] for proof in evidence.values()))
        assert earliest-1e-8 <= handoffs[0][i] < earliest+case['settings']['dt']+1e-8
    assert handoffs[0][1] > case['secondChangeSeconds'], 'This short schedule must wait for the last NPC at M2.'
    (EVIDENCE / (evidence_name+'.json')).write_text(json.dumps({'case':case,'controllers':evidence},indent=2),encoding='utf-8')


def check_challenges(w,used):
    w.click(148,747)  # Free results > home
    w.click(710,769)
    w.click(1080,829)  # PFL3 prediction
    w.save('15-eslalon')
    w.click(160,674)
    w.click(160,639)
    folder,case,b=wait_for(lambda:session_matches(used,90,'guided','B-SLALOM'),'Playable slalom did not finish.')
    assert case['datasetRole']=='showcase-challenge' and len(case['obstacles'])==6
    wait_for(lambda:w.primary_visible(65,540),'Slalom results were not drawn.')
    w.save('16-eslalon-meta')
    a,p=(csv_rows(folder/f'trace_{k}.csv') for k in ('Proportional','Pfl3'))
    peak=max(math.hypot(float(a0['x_m'])-float(p0['x_m']),float(a0['z_m'])-float(p0['z_m'])) for a0,p0 in zip(a,p))
    assert peak>2 and all(v['state']=='Success' for v in b.values())
    w.click(170,789)
    w.click(1040,769)
    w.click(160,674)
    w.click(160,639)
    folder,case,c=wait_for(lambda:session_matches(used,0,'guided','C-RELEVO'),'Playable relay did not finish.')
    assert case['datasetRole']=='showcase-challenge' and case['secondChangeSeconds']==16
    verify_relay_session(folder,case,c,'relevo-estandar')
    wait_for(lambda:w.primary_visible(65,540),'Relay results were not drawn.')
    w.save('17-relevo-meta')
    return b,c


def check_workshop(w,used):
    def at(x,z):
        return 708+x*652/21.3,465-z*652/21.3
    w.click(170,789)
    w.click(205,470)
    w.click(155,344)  # Disc
    w.click(*at(0,-8))  # Invalid: start gate
    w.click(*at(-3,0))
    w.click(1362,362)
    w.drag(at(-3,0),at(-4,-1))
    w.click(155,397)  # Rectangle
    w.click(*at(2,-2))
    w.click(1362,341)
    w.click(1362,541)
    w.click(155,450)  # Free polygon
    for v in [(0,2),(3,2),(3,3),(1,3),(1,5),(0,5)]:
        w.click(*at(*v))
    w.click(1230,365)
    w.click(155,727)
    w.click(155,727)  # Replace the same saved design atomically
    w.save('18-taller')
    w.click(155,661)
    w.click(155,639)
    folder,d,rows=wait_for(lambda:session_matches(used,0,'workshop','B-TALLER-*'),'Workshop did not export.')
    assert d['datasetRole']=='free-workshop' and len(d['obstacles'])==3 and len(rows)==2
    disc,box,poly=d['obstacles']
    assert math.isclose(disc['radius'],1.25) and abs(disc['center']['x']+4)<.2 and abs(disc['center']['z']+1)<.2
    width=max(v['x'] for v in box['vertices'])-min(v['x'] for v in box['vertices'])
    assert box['shape']==1 and math.isclose(box['rotation'],15,abs_tol=1e-8) and math.isclose(width,2.75), box
    assert poly['shape']==2 and len(poly['vertices'])==6
    assert d['start']=={'x':0,'z':-8} and d['goal']=={'x':0,'z':8}
    assert all(float(r['elapsed_s'])>0 for r in rows.values())
    saved=TRIALS.parent/'arena-b.json'
    assert json.loads(saved.read_bytes())['obstacles']==d['obstacles']
    w.click(155,674)  # Return to editor
    w.click(*at(-4,-1))
    w.click(1230,709)  # Delete disc
    w.click(155,661)
    w.click(155,639)
    _,edited,_=wait_for(lambda:session_matches(used,0,'workshop','B-TALLER-*'),'Edited workshop did not export.')
    assert len(edited['obstacles'])==2 and edited['configurationId']!=d['configurationId']
    return rows


def check_goal_editor(w, used):
    def at(x,z):
        return 708+x*652/21.3,465-z*652/21.3
    w.click(170,789)
    w.click(1140,771)  # Fixed relay > Edit goals
    w.click(165,565)
    w.click(*at(-3,4))
    w.drag(at(.8,-6.5),at(-4,-4))
    w.click(1130,364)
    w.click(1130,364)
    w.click(162,396)
    w.click(*at(5,5))
    for _ in range(5):
        w.click(1130,364)
    w.set_angle('-25,5',workshop=True)
    w.click(155,727)
    w.save('19-editor-metas')
    w.click(155,661)
    w.click(155,639)
    folder,d,rows=wait_for(lambda:session_matches(used,None,'workshop','C-TALLER-*'),'Custom relay did not finish.')
    assert d['datasetRole']=='free-workshop' and d['goalChangeSeconds']==8 and d['secondChangeSeconds']==11 and d['initialHeading']==-25.5
    for key,(x,z) in zip(('goal','secondGoal','thirdGoal'),[(-3,4),(-4,-4),(5,5)]):
        assert abs(d[key]['x']-x)<.2 and abs(d[key]['z']-z)<.2, (key,d[key])
    saved=json.loads((TRIALS.parent/'arena-c.json').read_bytes())
    assert all(saved[k]==d[k] for k in ('goal','secondGoal','thirdGoal','goalChangeSeconds','secondChangeSeconds'))
    verify_relay_session(folder,d,rows,'relevo-libre')
    wait_for(lambda:w.primary_visible(65,540),'Custom relay results were not drawn.')
    w.save('20-relevo-personalizado')
    w.click(160,674)
    w.click(160,661)
    w.click(155,639)
    _,replay,_=wait_for(lambda:session_matches(used,None,'workshop','C-TALLER-*'),'Replay from goal editor did not finish.')
    assert replay['configurationId']==d['configurationId']
    return d


def check_manual_headings(w,used):
    cases=[(240,'A-00-custom','-37,5',-37.5,'development-custom'),
           (720,'B-SLALOM-custom','27.25',27.25,'showcase-custom'),
           (1140,'C-RELEVO-custom','180',180,'showcase-custom')]
    for x,case,text,angle,role in cases:
        wait_for(lambda:w.primary_visible(65,540),'Expected results before switching circuits.')
        w.click(160,789)
        w.click(x,771)
        if x==240:
            w.set_angle('181')
            w.click(160,674)
            assert w.primary_visible(65,665), 'Invalid angle started the trial.'
        w.set_angle(text)
        if x==240:
            wait_for(lambda:w.heading_arrow_visible(angle),'The preview did not follow manual input.')
            w.save('21-angulo-manual')
        w.click(160,674)
        time.sleep(.7)
        w.click(160,698)
        folder,config,rows=wait_for(lambda:session_matches(used,angle,'guided',case),'Manual heading was not exported: '+case)
        assert config['datasetRole']==role
        for kind in rows:
            assert math.isclose(float(csv_rows(folder/f'trace_{kind}.csv')[0]['heading_deg']),angle,abs_tol=1e-8)


def check_gui(archive):
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    used = set(TRIALS.iterdir()) if TRIALS.exists() else set()
    with tempfile.TemporaryDirectory(prefix='npc-window-') as temp:
        with zipfile.ZipFile(archive) as zip_file:
            zip_file.extractall(temp)
        exe = Path(temp) / 'DrosophilaNavigationLab/DrosophilaNavigationLab.exe'
        cmd = [str(exe), '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '600',
               '-force-d3d11', '-force-driver-type-warp', '-logFile', str(EVIDENCE / 'Player.log')]
        proc = subprocess.Popen(cmd, cwd=exe.parent)
        window = None
        try:
            window = GameWindow(wait_for(lambda: player_window(proc.pid), 'No visible Player window.'))
            wait_for(lambda: window.primary_visible(448,730), 'Circuit selection was not drawn.')
            window.card_borders()
            window.resize(865,520)
            window.card_borders()
            window.save('00-menu-panoramico')
            window.resize(960,600)
            plate = window.crop(820,176,1344,503)
            time.sleep(1.2)
            assert ImageChops.difference(plate,window.crop(820,176,1344,503)).getbbox(), 'The live track was static.'
            window.save('01-menu')
            window.click(260, 768)
            wait_for(lambda: window.primary_visible(60, 674), 'Guide did not open.')
            wait_for(lambda: window.heading_arrow_visible(-45), 'Initial arrow is not visible at its world position.', 8)
            window.save('02-preparacion')
            window.click(160, 675)
            time.sleep(.8)
            window.click(135, 576)
            window.save('03-pausa')
            window.click(135, 576)
            window.click(135, 640)
            first_folder, initial, first = wait_for(lambda: session_matches(used, -45, 'guided', 'A-00'), 'First run did not export.')
            assert {r['state'] for r in first.values()} == {'Success'}
            assert {'pause', 'resume'} <= {r['event'] for r in csv.DictReader((first_folder / 'events_Pfl3.csv').open(encoding='utf-8-sig'))}
            window.save('04-resultados')
            window.click(175, 554)
            window.click(170, 429)
            wait_for(lambda: window.heading_arrow_visible(45), 'Arrow did not turn towards the goal.', 8)
            window.click(170, 674)
            _, changed, second = wait_for(lambda: session_matches(used, 45, 'guided', 'A-00-custom'), 'Changed heading did not export.')
            for key in initial.keys() - {'initialHeading', 'configurationId', 'datasetRole'}:
                assert initial[key] == changed[key], key
            assert {r['state'] for r in second.values()} == {'Success'}
            assert all(float(second[k]['arrival_s']) < float(first[k]['arrival_s']) for k in first)
            window.save('05-comparacion')
            window.click(170, 554)
            window.click(170, 502)
            wait_for(lambda: window.heading_arrow_visible(-135), 'Arrow did not turn away from the goal.', 8)
            window.click(170, 674)
            opposite_folder, _, opposite = wait_for(lambda: session_matches(used, -135, 'guided', 'A-00-custom'), 'Antipodal run did not export.', 90)
            assert opposite['Pfl3']['state'] == 'Timeout' and not opposite['Pfl3']['arrival_s']
            assert opposite['Proportional']['state'] == 'Success'
            trace = csv_rows(opposite_folder / 'trace_Pfl3.csv')
            assert all(abs(float(s['omega_deg_s'])) < 1e-6 for s in trace if float(s['time_s']) < 1)
            window.save('06-rumbo-opuesto')
            window.click(175, 554)
            window.click(170, 565)
            window.click(160, 674)
            _, _, near = wait_for(lambda: session_matches(used,-134,'guided','A-00-custom'), 'Near-opposite run did not finish.', 90)
            assert near['Pfl3']['state'] == 'Success' and near['Proportional']['state'] == 'Success'
            window.save('07-casi-de-espaldas')
            obstacles, changed_goal = check_other_scenarios(window, used)
            window.click(171, 674)
            window.click(650, 235)
            window.click(300, 790)
            time.sleep(1)
            window.click(135, 634)
            _, _, free = wait_for(lambda: session_matches(used, None, 'free', 'B-00'), 'Free B-00 did not export.')
            assert {r['state'] for r in free.values()} == {'Aborted'}
            slalom,relay=check_challenges(window,used)
            workshop=check_workshop(window,used)
            custom=check_goal_editor(window,used)
            proc.terminate()
            proc.wait(timeout=15)
            proc=subprocess.Popen(cmd[:-1]+[str(EVIDENCE/'Player-reapertura.log')],cwd=exe.parent)
            window=GameWindow(wait_for(lambda:player_window(proc.pid),'Player did not reopen the workshop.'))
            wait_for(lambda:window.primary_visible(448,730),'Title was not ready after restart.')
            window.click(220,470)
            window.click(155,661)
            window.click(155,639)
            _,restored,_=wait_for(lambda:session_matches(used,0,'workshop','B-TALLER-*'),'Saved arena did not reopen.')
            assert len(restored['obstacles'])==3 and restored['obstacles'][2]['shape']==2
            wait_for(lambda:window.primary_visible(65,540),'Restored B results were not drawn.')
            window.click(160,789)
            window.click(500,470)
            window.click(155,661)
            window.click(155,639)
            _,restored_c,_=wait_for(lambda:session_matches(used,None,'workshop','C-TALLER-*'),'Saved relay did not reopen.')
            assert restored_c==custom
            wait_for(lambda:window.primary_visible(65,540),'Restored C results were not drawn.')
            window.click(160,789)
            window.click(1140,771)
            window.click(160,674)
            window.click(155,639)
            _,fixed,_=wait_for(lambda:session_matches(used,0,'guided','C-RELEVO'),'Fixed relay lost after editing.')
            assert fixed['goalChangeSeconds']==10 and fixed['secondChangeSeconds']==16 and fixed['goal']=={'x':0,'z':6.5}
            check_manual_headings(window,used)
            print('PASS: three circuit entrances, both workshops, independent saves, timed relays, pilot and exports.', flush=True)
            report = {name: {key: {'state': row['state'], 'arrival_s': row['arrival_s']} for key, row in rows.items()}
                      for name, rows in [('lateral', first), ('hacia_meta', second), ('opuesto', opposite), ('casi_opuesto', near), ('obstaculos', obstacles), ('cambio_meta', changed_goal), ('libre', free), ('eslalon',slalom),('relevo',relay),('taller',workshop)]}
            (EVIDENCE / 'resultados.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        finally:
            if window is not None:
                window.save('ultimo-estado')
            if proc.poll() is None:
                proc.terminate()
                proc.wait(timeout=15)
    log = '\n'.join(p.read_text(encoding='utf-8', errors='replace') for p in EVIDENCE.glob('Player*.log'))
    for fault in ['NullReferenceException', 'MissingMethodException', 'TypeLoadException', 'IndexOutOfRangeException',
                  'MissingReferenceException', 'Serialization layout', 'Could not load file or assembly']:
        assert fault.casefold() not in log.casefold(), fault


if __name__ == '__main__':
    check_gui(Path(sys.argv[1]))
