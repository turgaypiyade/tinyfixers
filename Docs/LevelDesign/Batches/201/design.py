"""201: BirdHouse introduction; data only, no gameplay simulation."""
W, H = 9, 10
KEYS = ('cells', 'obstacles', 'obstacleOrigins', 'wallPieceIds',
        'hamsterSatietyByCell', 'pinnedTileTypes', 'pinnedSpecialTypes')
GROUPS = [(6, 0, 2, 3), (1, 7, 2, 3)]  # zero-based: upper right / lower left, a matching lane on both sides
DATA = {k: [(-1 if k == 'obstacleOrigins' else 1 if k == 'cells' else 0)] * (W*H) for k in KEYS}
DATA['cells'][0] = DATA['cells'][-1] = 0
for x0, y0, width, height in GROUPS:
    for y in range(y0, y0+height):
        for x in range(x0, x0+width):
            i = y*W+x
            DATA['obstacles'][i] = 78
            DATA['obstacleOrigins'][i] = i
DESIGN = dict(n=201, title='Guguk Bahçesi', nameEn='Cuckoo Garden', w=W, h=H,
              moves=26, birdGoal=50, data=DATA, groups=GROUPS)
