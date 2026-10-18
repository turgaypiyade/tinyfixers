# Static Unity asset/palette/goal registration checks. No Unity execution or build.
require 'yaml'
require 'digest'

def unity_yaml(path)
  YAML.safe_load(File.readlines(path).reject { |line| line.start_with?('%', '--- !u!') }.join)
end

def check(value, message)
  raise message unless value
end

library = unity_yaml('Assets/_Project/Settings/ObstacleLibrary.asset')['MonoBehaviour']['obstacles']
check(library.map { |o| o['id'] }.uniq.size == library.size, 'Duplicate obstacle ID')
statue = library.find { |o| o['id'] == 70 }
check(statue && statue['size'] == { 'x' => 2, 'y' => 2 } && statue['hits'] == 4, '2x2, four-hit definition missing')
check(statue['stages'].size == 4, 'Wrong number of stages')
check(statue['allowCrossContextFallback'] == 0 && statue['exitAtBottom'] == 0, 'Wrong fallback/exit flags')
statue['stages'].each do |stage|
  check(stage['damageRule'] == 1 && stage['behavior'] == 1 && stage['blocksCells'] == 1, 'Each stage must be a special-only static blocker')
  check(stage['allowDiagonal'] == 1 && stage['hitParticleSprites'] == [], 'Corner flow / shared particle fallback')
end
path = 'Assets/_Project/Art/UI/Obstacles/RhodesStatue/RhodesStatue_Stages.png'
meta = unity_yaml(path + '.meta')
importer = meta['TextureImporter']
sprites = importer['spriteSheet']['sprites']
check(importer['spriteMode'] == 2 && importer['alphaIsTransparency'] == 1 && importer['isReadable'] == 0, 'Sprite-sheet import flags')
check(sprites.size == 4, 'Missing frame')
statue['stages'].each_with_index do |stage, i|
  sprite = sprites[i]
  base_meta = unity_yaml("Assets/_Project/Resources/RhodesStatue/S#{i + 1}_Base.png.meta")
  check(base_meta['TextureImporter']['spriteMode'] == 1 && base_meta['TextureImporter']['textureType'] == 8, 'Base must import as a single sprite')
  check(stage['sprite'] == { 'fileID' => 21300000, 'guid' => base_meta['guid'], 'type' => 3 }, 'Stage must reference its Resources base sprite')
  check(sprite['rect'].slice('x', 'y', 'width', 'height') == { 'x' => i * 543, 'y' => 0, 'width' => 543, 'height' => 724 }, 'Frame sizing/alignment')
  check(sprite['pivot'] == { 'x' => 0.5, 'y' => 0.5 }, 'Inconsistent pivot')
  check(importer['spriteSheet']['nameFileIdTable'][sprite['name']] == sprite['internalID'], 'Importer ID table mismatch')
end
goal_meta = unity_yaml('Assets/_Project/Resources/RhodesStatue/S1_Base.png.meta')
check(statue['goalIconSprite'] == { 'fileID' => 21300000, 'guid' => goal_meta['guid'], 'type' => 3 }, 'Goal/prelevel icon must reference Resources/RhodesStatue/S1_Base')
%w[hitParticleSprites breakParticleSprites].each { |key| check(statue[key].size == 6, 'Shared stone chips missing') }
entry = unity_yaml('Assets/_Project/Data/Tutorial/ObstacleHintLibrary.asset')['MonoBehaviour']['entries'].find { |e| e['id'] == 70 }
check(entry && entry['titleLocKey'] == 'hint_obstacle_rhodes_statue_name', 'Hint missing')
require 'json'
JSON.parse(File.read('Assets/_Project/Resources/Localization/tinyfixers_localization.json'))['languages'].each do |lang|
  %w[hint_obstacle_rhodes_statue_name hint_obstacle_rhodes_statue welcome_back_reward_title].each do |key|
    check(lang['entries'].count { |e| e['key'] == key && !e['value'].empty? } == 1, 'Localization missing/duplicated')
  end
end
catalog = unity_yaml('Assets/_Project/Settings/LevelCatalogPro.asset')['MonoBehaviour']
%w[001600 001610].each do |id|
  level = unity_yaml("Assets/_Project/Settings/ProductionLevels/LevelP_#{id}.asset")['MonoBehaviour']
  n = level['width'] * level['height']
  %w[cells obstacles obstacleOrigins pinnedTileTypes pinnedSpecialTypes wallPieceIds hamsterSatietyByCell].each do |key|
    raw = File.read("Assets/_Project/Settings/ProductionLevels/LevelP_#{id}.asset")
    encoded = raw[/^  #{key}: ([0-9a-f]+)$/, 1]
    check(encoded && [encoded].pack('H*').bytesize == n * 4, 'Malformed level array')
  end
end
puts 'PASS: Unity YAML, 2x2/four-stage rules, Resources stage sprites, legacy sheet rects, S1_Base goal/prelevel icon, stone chips, TR/EN hint and level arrays.'
