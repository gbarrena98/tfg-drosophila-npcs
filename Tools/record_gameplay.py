"""Record the PEC3 demonstration from the visible Windows Player."""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import time
import zipfile

import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont
from test_guided_player import GameWindow, player_window, wait_for

FPS = 12
WIDTH, HEIGHT = 960, 680
GAME_HEIGHT = 600


class Recorder:
    def __init__(self, window, destination):
        self.window = window
        self.frames = 0
        fonts = Path(os.environ['WINDIR']) / 'Fonts'
        self.heading = ImageFont.truetype(str(fonts / 'segoeuib.ttf'), 23)
        self.body = ImageFont.truetype(str(fonts / 'segoeui.ttf'), 21)
        command = [imageio_ffmpeg.get_ffmpeg_exe(), '-hide_banner', '-loglevel', 'error', '-y',
                   '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{WIDTH}x{HEIGHT}', '-r', str(FPS),
                   '-i', '-', '-an', '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '18',
                   '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(destination)]
        self.encoder = subprocess.Popen(command, stdin=subprocess.PIPE)

    def clip(self, seconds, title, lines, action=None):
        print(f'{self.frames/FPS:.0f}s  {title}', flush=True)
        base = Image.new('RGB', (WIDTH, HEIGHT), '#14252a')
        draw = ImageDraw.Draw(base)
        draw.rectangle((0, GAME_HEIGHT, WIDTH-1, GAME_HEIGHT+1), fill='#67d7cf')
        draw.text((21, GAME_HEIGHT+23), title, font=self.heading, fill='#8fe5db')
        for index, line in enumerate(lines):
            assert draw.textlength(line, font=self.body) < WIDTH-250, line
            draw.text((235, GAME_HEIGHT+9+29*index), line, font=self.body, fill='#f1f4ee')
        count = round(seconds*FPS)
        with ThreadPoolExecutor(max_workers=1) as executor:
            task = executor.submit(action) if action else None
            start = time.monotonic()
            index = 0
            while index < count:
                shot = self.window.capture().convert('RGB')
                if shot.size != (WIDTH, GAME_HEIGHT):
                    shot = shot.resize((WIDTH, GAME_HEIGHT), Image.Resampling.LANCZOS)
                base.paste(shot, (0, 0))
                raw = base.tobytes()
                due = min(count, max(index+1, round((time.monotonic()-start)*FPS)))
                for _ in range(index, due):
                    self.encoder.stdin.write(raw)
                index = due
                time.sleep(max(0, start+index/FPS-time.monotonic()))
            if task:
                task.result()
        self.frames += count

    def close(self):
        self.encoder.stdin.close()
        if self.encoder.wait(timeout=60) != 0:
            raise RuntimeError('The demonstration could not be encoded.')


def demonstration(w, film):
    def choose(x):
        w.click(x, 768)
        wait_for(lambda: w.primary_visible(60, 674), 'Circuit did not open.')
    def results():
        wait_for(lambda: w.primary_visible(65, 540), 'Result screen missing.')
    def home():
        w.click(160, 789)
        wait_for(lambda: w.primary_visible(448, 730), 'Menu missing.')
    def at(x, z):
        return 708+x*652/21.3, 465-z*652/21.3
    def heading_and_prediction():
        time.sleep(.9)
        w.set_angle('-30,5')
        w.click(1260, 828)
    def edit_obstacle():
        w.click(155, 344)
        w.click(*at(5, 1))
        w.click(1362, 362)
        w.drag(at(5, 1), at(5, 3))
    def edit_beacon():
        w.click(155, 396)
        w.click(*at(5, -2))
        for _ in range(3):
            w.click(1362, 364)
        w.click(155, 727)

    film.clip(5, 'Selección de pista',
              ['Dos NPC comparten la salida y la meta. Elige un circuito',
               'para comparar el control proporcional con el modelo PFL3.'])
    choose(245)
    film.clip(6, 'A · Rumbo de salida',
              ['Aquí escribimos −30,5° y pronosticamos un empate.',
               'La flecha blanca muestra la orientación inicial de ambos NPC.'], heading_and_prediction)
    w.click(160, 674)
    film.clip(5, 'A · Espacio abierto',
              ['El cuadrado ámbar sigue al proporcional y el punto cian al PFL3.',
               'Las dos trazas conservan el camino recorrido.'])
    w.click(155, 639)
    results()
    film.clip(4, 'A · Llegada',
              ['La tabla compara los tiempos y las distancias. Puedes repetir',
               'el mismo rumbo o cambiarlo y contrastar el resultado.'])
    home()
    choose(715)
    film.clip(4, 'B · Obstáculos',
              ['Cuatro discos y dos barreras forman el zigzag. En el reto',
               'fijo ambos salen con el mismo rumbo, a 90°.'])
    w.click(160, 674)
    film.clip(8, 'B · Dos recorridos',
              ['Sigue la zona central. Cada NPC puede rodear una barrera',
               'por un lado distinto, aunque los tiempos de llegada se parezcan.'])
    results()
    film.clip(5, 'B · Resultado',
              ['En este circuito se separan hasta 3,19 m y terminan con',
               'solo 0,16 s de diferencia. Los filtros permiten aislar cada ruta.'])
    home()
    choose(1135)
    w.click(160, 674)
    film.clip(5, 'C · Primera baliza',
              ['La bandera verde señala el objetivo activo. Ambos deben entrar',
               'en el círculo de cada baliza para abrir la siguiente.'])
    wait_for(lambda: w.goal_is_green(.8, -6.5), 'M2 was not seen in the recording.', 24)
    film.clip(4, 'C · Segunda baliza',
              ['A los 10 s se activa M2. Los dos NPC dan la vuelta y tratan',
               'de recuperar el rumbo hacia la nueva posición.'])
    film.clip(8, 'C · Paso por M2',
              ['El proporcional llega primero y espera dentro del círculo.',
               'M3 se abre cuando también llega el PFL3.'])
    wait_for(lambda: w.goal_is_green(4.5, 7), 'M3 was not seen in the recording.', 20)
    film.clip(5, 'C · Última baliza',
              ['Los dos han tocado M2. Ahora deben alcanzar la tercera baliza.',
               'El tiempo de llegada se registra al entrar en ese último círculo.'])
    results()
    film.clip(5, 'C · Reorientación',
              ['Ambos han tocado las tres balizas. Compara el tiempo de llegada',
               'con el que cada NPC tardó en orientarse tras cada cambio.'])
    home()
    w.click(220, 470)
    film.clip(7, 'Taller de obstáculos',
              ['Elige una pieza, colócala en la pista y ajusta sus dimensiones.',
               'También puedes dibujar un polígono o arrastrar una pieza.'], edit_obstacle)
    w.click(155, 815)
    w.click(510, 470)
    film.clip(7, 'Editor de metas',
              ['Selecciona M3, pulsa en la pista y ajusta su tiempo mínimo.',
               'También en modo libre el relevo espera a los dos NPC.'], edit_beacon)
    w.click(155, 661)
    w.click(155, 639)
    results()
    film.clip(5, 'Resultados y CSV',
              ['«Abrir carpeta CSV» reúne configuración, trayectorias y métricas.',
               '«Volver al editor» permite probar otra variante del mismo circuito.'])


def main(archive, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='npc-demo-') as folder:
        with zipfile.ZipFile(archive) as zipped:
            zipped.extractall(folder)
        exe = Path(folder) / 'DrosophilaNavigationLab/DrosophilaNavigationLab.exe'
        process = subprocess.Popen([str(exe), '-screen-fullscreen', '0', '-screen-width', '960',
                                    '-screen-height', '600', '-force-d3d11', '-force-driver-type-warp',
                                    '-logFile', str(Path(folder)/'Player.log')], cwd=exe.parent)
        try:
            w = GameWindow(wait_for(lambda: player_window(process.pid), 'Could not open the Player.'))
            wait_for(lambda: w.primary_visible(448, 730), 'Circuit menu missing.')
            film = Recorder(w, destination)
            try:
                demonstration(w, film)
            finally:
                film.close()
            assert 0 < film.frames/FPS <= 120
            print(f'Gameplay MP4: {film.frames/FPS:.1f} s / {film.frames} frames.')
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=15)


if __name__ == '__main__':
    main(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
