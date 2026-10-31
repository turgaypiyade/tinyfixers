"""Create Gift Festival config/art references and append TR/EN strings without touching other content."""
from pathlib import Path
import json
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/_Project/Art/UI/MainScreenEvents/GiftFestival"
SCRIPTS = ROOT / "Assets/_Project/Scripts/Events/GiftFestival"
RESOURCES = ROOT / "Assets/_Project/Resources/Events"


def meta(path, kind="DefaultImporter", file_id=None):
    p = Path(str(path) + ".meta")
    if not p.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "tinyfixers/" + str(path.relative_to(ROOT))).hex
        text = f"fileFormatVersion: 2\nguid: {guid}\n"
        if path.is_dir(): text += "folderAsset: yes\n"
        text += f"{kind}:\n  externalObjects: {{}}\n"
        if file_id is not None: text += f"  mainObjectFileID: {file_id}\n"
        p.write_text(text + "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return re.search(r"^guid: (\w+)", p.read_text(), re.M).group(1)


def ref(path, file_id=21300000, asset_type=3):
    return f"{{fileID: {file_id}, guid: {meta(path)}, type: {asset_type}}}"


def so_header(name, script):
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {ref(SCRIPTS/script, 11500000)}
  m_Name: {name}
  m_EditorClassIdentifier: Assembly-CSharp::{name}
"""


def create(path, text, kind="NativeFormatImporter", file_id=11400000):
    if not path.exists(): path.write_text(text)
    meta(path, kind, file_id)


def main():
    meta(SCRIPTS)
    for p in SCRIPTS.glob("*.cs"): meta(p, "MonoImporter")
    meta(ROOT / "Assets/_Project/Scripts/Editor/GiftFestivalDebugMenu.cs", "MonoImporter")
    config = so_header("GiftFestivalConfig", "GiftFestivalConfig.cs") + """  unlockAfterLevel: 100
  activeHours: 48
  restHours: 120
  debugForceAvailable: 0
  redWidth: 0.15
  greenWidth: 0.2
  sweepSeconds: 1.05
  shotSpeed: 1.35
  speedIncreasePerRound: 0.2
  emptyBoxFraction: 0.25
  rounds:
  - size: 2
    boxCoins: 25
    completionCoins: 0
    boxRewards:
    - type: 0
      amount: 25
      durationMinutes: 0
    - type: 13
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 1
      durationMinutes: 0
    completionRewards:
    - type: 20
      amount: 1
      durationMinutes: 0
  - size: 3
    boxCoins: 40
    completionCoins: 0
    boxRewards:
    - type: 0
      amount: 40
      durationMinutes: 0
    - type: 13
      amount: 1
      durationMinutes: 0
    - type: 23
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 1
      durationMinutes: 0
    - type: 0
      amount: 60
      durationMinutes: 0
    - type: 11
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 1
      durationMinutes: 0
    completionRewards:
    - type: 0
      amount: 100
      durationMinutes: 0
    - type: 11
      amount: 1
      durationMinutes: 0
  - size: 4
    boxCoins: 60
    completionCoins: 0
    boxRewards:
    - type: 0
      amount: 50
      durationMinutes: 0
    - type: 0
      amount: 75
      durationMinutes: 0
    - type: 0
      amount: 100
      durationMinutes: 0
    - type: 13
      amount: 1
      durationMinutes: 0
    - type: 11
      amount: 1
      durationMinutes: 0
    - type: 12
      amount: 1
      durationMinutes: 0
    - type: 20
      amount: 1
      durationMinutes: 0
    - type: 23
      amount: 1
      durationMinutes: 0
    - type: 24
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 2
      durationMinutes: 0
    completionRewards:
    - type: 12
      amount: 1
      durationMinutes: 0
    - type: 24
      amount: 1
      durationMinutes: 0
    - type: 2
      amount: 2
      durationMinutes: 0
  autoShowJoinPopup: 1
  introSeconds: 2.6
"""
    create(RESOURCES / "GiftFestivalConfig.asset", config)
    material = ART / "GiftFestivalDisabled.mat"
    create(material, f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: GiftFestivalDisabled
  m_Shader: {ref(ROOT/'Assets/_Project/Shaders/UI_PopupColorRemap.shader', 4800000)}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _HueRange: 0.13
    - _HueSoftness: 0.03
    - _StencilComp: 8
    - _Stencil: 0
    - _StencilOp: 0
    - _StencilWriteMask: 255
    - _StencilReadMask: 255
    - _ColorMask: 15
    - _UseUIAlphaClip: 0
    m_Colors:
    - _Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _SourceColor: {{r: 1, g: 0.82, b: 0.03, a: 1}}
    - _TargetColor: {{r: 0.67, g: 0.65, b: 0.59, a: 1}}
    - _SourceDarkColor: {{r: 0.7, g: 0.43, b: 0.01, a: 1}}
    - _TargetDarkColor: {{r: 0.37, g: 0.35, b: 0.31, a: 1}}
""", file_id=2100000)
    art = so_header("GiftFestivalArt", "GiftFestivalArt.cs")
    singles = {"menuLogo":"GiftFestivalLogo.png", "background":"Background.png", "topSign":"TopSign.png", "board":"Board.png", "glass":"Glass.png", "giftSymbol":"GiftSymbol.png", "introPanorama":"stadium.png", "ball":"Baseball.png", "hitNormal":"HitButton.png", "hitFrame":"HitFrame.png", "hitPressed":"HitPressed.png"}
    for field, filename in singles.items(): art += f"  {field}: {ref(ART/filename)}\n"
    for field, filename, count in [("metalCorners","MetalCorners.png",4),("targetCorners","TargetFrame.png",4),("ballSpin","BaseballSpin.png",5)]:
        art += f"  {field}:\n"
        for i in range(count): art += f"  - {ref(ART/filename,21300000+i)}\n"
    art += "  bearFrames:\n"
    for i in range(1,7): art += f"  - {ref(ART/f'Bear/BBB{i}.png')}\n"
    art += "  rabbitFrames:\n"
    for i in range(1,5): art += f"  - {ref(ART/f'Rabbit/RB{i}.png')}\n"
    art += f"  - {ref(ART/'Rabbit/RB5V1.png')}\n"
    art += f"  closeIcon: {ref(ROOT/'Assets/_Project/Art/UI/BtnClose.png')}\n"
    art += f"  font: {ref(ROOT/'Assets/_Project/Fonts/TinyFixersPop/TinyFixersPop SDF.asset',11400000,2)}\n"
    art += f"  fontMaterial: {ref(ROOT/'Assets/_Project/Fonts/Materials/TinyFixersPop_OnOrange.mat',2100000,2)}\n"
    art += f"  disabledButtonMaterial: {ref(material,2100000,2)}\n"
    create(RESOURCES / "GiftFestivalArt.asset", art)

    strings = {
        "title": ("HEDİYE ŞENLİĞİ", "GIFT FESTIVAL"),
        "menu_title": ("HEDİYE\nŞENLİĞİ", "GIFT\nFESTIVAL"),
        "hit": ("VUR!", "HIT!"),
        "ball": ("Beyzbol topu", "Baseball"),
        "join_summary": (
            "Kazandığın her bölüm 1 top! Yeşili yakala, hediyeleri aç!",
            "Every level won earns 1 ball! Stop on green to collect gifts!"),
        "join_message": (
            "Şenliğe katıl, hediye kutularını aç!\n\nKatıldıktan sonra kazandığın her bölüm sana 1 top verir.\n\nHedefini seç, yeşili yakala ve VUR!",
            "Join the festival and open gift boxes!\n\nAfter joining, every level you win gives you 1 ball.\n\nChoose your target, stop on green and HIT!"),
        "round": ("TUR {0}/{1}", "ROUND {0}/{1}"),
        "select": ("Lütfen bir hediye seçin", "Please choose a gift"),
        "tutorial_select": ("İlk topun bizden!\nİşaretli hediyeyi seç.", "Your first ball is on us!\nChoose the highlighted gift."),
        "tutorial_hit": ("Gösterge yeşile geldiğinde\nVUR! düğmesine dokun.", "When the indicator reaches green,\ntap HIT!"),
        "time_hit": ("YEŞİLİ YAKALA, HEDEFİ VUR!", "STOP ON GREEN TO HIT YOUR TARGET!"),
        "earn_balls": ("SEVİYE GEÇ, TOP KAZAN!", "WIN LEVELS TO EARN BALLS!"),
        "ended": ("ŞENLİK SONA ERDİ", "THE FESTIVAL HAS ENDED"),
        "finished": ("BÜTÜN HEDİYELER SENİN!", "YOU COLLECTED EVERY GIFT!"),
        "miss": ("ISKA!", "MISS!"),
        "perfect": ("TAM İSABET!", "PERFECT HIT!"),
        "deflected": ("HEDEF ŞAŞTI!", "DEFLECTED!"),
        "already_open": ("BU KUTU ZATEN AÇIK!", "THAT BOX IS ALREADY OPEN!"),
        "empty_box": ("BOŞ KUTU!", "EMPTY BOX!"),
        "round_complete": ("TAHTA TAMAMLANDI!", "BOARD COMPLETE!"),
        "next_board": ("Sıradaki tahta seni bekliyor!", "Your next board is ready!"),
        "skip_intro": ("GEÇMEK İÇİN DOKUN", "TAP TO SKIP"),
        "howto": (
            "Bir hediye kutusu seç ve gösterge yeşildeyken VUR!\n\nYeşil: seçtiğin kutu. Sol sarı: bir alt kutu. Sağ sarı: bir üst kutu. Kenarda sapma tahtanın içine döner. Kırmızı: ıska!\n\nHer atış bir top harcar. Açılmış bir kutuya isabet edersen yeni ödül kazanmazsın. Tahtadaki tüm kutuları aç, büyük ödülü al ve daha büyük tahtaya geç.\n\nEtkinliğe katıldıktan sonra kazandığın her bölüm 1 top verir. Kaybedersen biriktirdiğin toplar silinmez. Şenlik 2 gün sürer, ardından 5 gün ara verir. Yeni şenlikte yeniden katılman gerekir.\n\nKutularda altın, joker, güçlendirici veya yıldız var. Yaklaşık dört kutudan biri boş; boş kutu da top harcar ve açılır.",
            "Choose a gift box and HIT while the indicator is on green!\n\nGreen: your chosen box. Left yellow: one box below. Right yellow: one box above. At an edge, deflection turns back into the board. Red: a miss!\n\nEach shot costs one ball. Hitting an already opened box gives no new reward. Open every box to collect the board bonus and advance to a bigger board.\n\nAfter joining, every level you win gives you 1 ball. Losing a level does not remove your banked balls. The festival lasts 2 days, followed by a 5-day break. Join again when the next festival starts.\n\nBoxes contain coins, jokers, boosters or stars. About one in four boxes is empty; an empty box still costs one ball and opens.")
    }
    localization = ROOT / "Assets/_Project/Resources/Localization/tinyfixers_localization.json"
    contents = json.loads(localization.read_text())
    for language in contents["languages"]:
        if language["code"] not in ("tr", "en"): continue
        col = 0 if language["code"] == "tr" else 1
        existing = {x["key"]: x for x in language["entries"]}
        for key, values in strings.items():
            full = "giftfestival_" + key
            if full in existing: existing[full]["value"] = values[col]
            else: language["entries"].append({"key": full, "value": values[col]})
    localization.write_text(json.dumps(contents, ensure_ascii=False, indent=2)+"\n")
    print("Gift Festival settings, stadium intro, logo, font, sprite references and TR/EN strings configured.")


if __name__ == "__main__": main()
