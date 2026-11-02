#!/usr/bin/env python3
"""Download WORLD DEM tiles (zoom 0-8) from Mapterhorn, webp->png"""
import math, os, json, time, urllib.request, urllib.error, ssl
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image
from io import BytesIO

DEST = "/Users/cjh/Downloads/Projects/AppPlat/AppPlat8/App/wwwroot/tiles/dem"
MAX_Z = 8
CONCURRENCY = 30
TILEJSON_URL = "https://tiles.mapterhorn.com/tilejson.json"

def fetch_tilejson():
    ctx = ssl.create_default_context()
    req = urllib.request.Request(TILEJSON_URL, headers={'User-Agent':'Mozilla/5.0'})
    with urllib.request.urlopen(req, timeout=30, context=ctx) as r:
        return json.loads(r.read())

def calc_tiles():
    tiles = []
    for z in range(0, MAX_Z+1):
        n = 2**z
        for x in range(n):
            for y in range(n):
                tiles.append((z, x, y))
    return tiles

def download_tile(args):
    z, x, y, url_tpl = args
    out = os.path.join(DEST, str(z), str(x), f"{y}.png")
    if os.path.exists(out) and os.path.getsize(out) > 0:
        return ('skip', out)
    url = url_tpl.format(z=z, x=x, y=y)
    ctx = ssl.create_default_context()
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={'User-Agent':'Mozilla/5.0', 'Accept':'*/*'})
            with urllib.request.urlopen(req, timeout=15, context=ctx) as r:
                data = r.read()
            os.makedirs(os.path.dirname(out), exist_ok=True)
            if url.endswith('.webp'):
                img = Image.open(BytesIO(data))
                if img.mode not in ('RGB','RGBA'):
                    img = img.convert('RGB')
                img.save(out, 'PNG')
            else:
                with open(out, 'wb') as f:
                    f.write(data)
            return ('ok', out)
        except urllib.error.HTTPError as e:
            if e.code == 404: return ('404', url)
            time.sleep(0.5 * (attempt+1))
        except Exception as e:
            time.sleep(0.5 * (attempt+1))
    return ('fail', url)

def main():
    print(f"=== 全球 DEM 瓦片下载 (zoom 0-{MAX_Z}) ===")
    print(f"目标: {DEST}")
    print(f"并发: {CONCURRENCY}")
    print()
    
    print("[1/3] 获取 TileJSON...")
    tj = fetch_tilejson()
    url_tpl = tj['tiles'][0]
    print(f"  tiles: {url_tpl}")
    print(f"  encoding: {tj.get('encoding','?')}")
    
    print(f"\n[2/3] 计算瓦片...")
    all_tiles = calc_tiles()
    print(f"  总计: {len(all_tiles)} tiles")
    
    tasks = [(z,x,y,url_tpl) for z,x,y in all_tiles]
    
    print(f"\n[3/3] 下载中...")
    ok = skip = fail = n404 = 0
    t0 = time.time()
    with ThreadPoolExecutor(max_workers=CONCURRENCY) as pool:
        futures = {pool.submit(download_tile, t): t for t in tasks}
        done_count = 0
        for fut in as_completed(futures):
            status, info = fut.result()
            done_count += 1
            if status == 'ok': ok += 1
            elif status == 'skip': skip += 1
            elif status == '404': n404 += 1
            else: fail += 1
            if done_count % 1000 == 0 or done_count == len(tasks):
                elapsed = time.time() - t0
                rate = done_count / elapsed if elapsed > 0 else 0
                print(f"  [{done_count}/{len(tasks)}] {done_count/len(tasks)*100:.1f}% | +{ok} ={skip} 404:{n404} -{fail} | {rate:.0f}/s")
    
    total_time = time.time() - t0
    total_tiles = 0
    total_bytes = 0
    for d1 in os.listdir(DEST):
        if not d1.isdigit(): continue
        for d2 in os.listdir(os.path.join(DEST, d1)):
            if not d2.isdigit(): continue
            for d3 in os.listdir(os.path.join(DEST, d1, d2)):
                if d3.endswith('.png'):
                    total_tiles += 1
                    total_bytes += os.path.getsize(os.path.join(DEST, d1, d2, d3))
    
    print(f"\n=== 完成 ===")
    print(f"  新增:{ok}  跳过:{skip}  404:{n404}  失败:{fail}")
    print(f"  耗时: {total_time:.0f}s")
    print(f"  现有: {total_tiles} tiles, {total_bytes/1024/1024:.0f} MB")

if __name__ == '__main__':
    main()