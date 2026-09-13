// 把散装素材合并成 content/lines.json（中文）或 content/lines.en.json（英文）
//   用法：node tools/build-lines.mjs         → 中文（_meta + _keywords + _env1/_env2/_chat1/_chat2）
//         node tools/build-lines.mjs en      → 英文（_meta.en + _keywords.en + _en1.._en4）
//   输出：content/lines.json 或 content/lines.en.json（程序运行时只读这一个文件）
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const content = path.join(root, 'content')

const lang = process.argv[2] === 'en' ? 'en' : 'zh'
const readJson = (f) => JSON.parse(fs.readFileSync(path.join(content, f), 'utf8'))

const meta = readJson(lang === 'en' ? '_meta.en.json' : '_meta.json')
const keywords = readJson(lang === 'en' ? '_keywords.en.json' : '_keywords.json')
const sources = lang === 'en'
  ? ['_en1.json', '_en2.json', '_en3.json', '_en4.json']
  : ['_env1.json', '_env2.json', '_chat1.json', '_chat2.json']
const outName = lang === 'en' ? 'lines.en.json' : 'lines.json'
console.log(`  语言：${lang === 'en' ? 'English' : '中文'}`)

const categories = {}
const owner = {}
for (const f of sources) {
  const data = readJson(f)
  const cats = data.categories || {}
  for (const [name, lines] of Object.entries(cats)) {
    if (categories[name]) {
      console.warn(`  [合并] 分类 ${name} 在 ${owner[name]} 和 ${f} 里都有，已合并去重`)
      for (const line of lines) {
        const text = typeof line === 'string' ? line : line.text
        const dup = categories[name].some((x) => (typeof x === 'string' ? x : x.text) === text)
        if (!dup) categories[name].push(line)
      }
    } else {
      categories[name] = lines.slice()
      owner[name] = f
    }
  }
}

// 关键词指向的分类必须存在
let bad = 0
for (const [cat, words] of Object.entries(keywords)) {
  if (cat.startsWith('_')) continue
  if (!categories[cat]) { console.error(`  [错误] 关键词指向了不存在的分类：${cat}`); bad++ }
  if (!Array.isArray(words) || words.length === 0) { console.error(`  [错误] ${cat} 的关键词列表是空的`); bad++ }
}
for (const cat of meta.chat_fallback) {
  if (!categories[cat]) { console.error(`  [错误] 兜底分类不存在：${cat}`); bad++ }
}
// 引擎会直接调用的分类
const required = [
  'boot', 'greet_morning', 'greet_afternoon', 'greet_evening', 'greet_night', 'welcome_back',
  'idle', 'idle_3min', 'idle_5min', 'idle_8min', 'idle_12min', 'wake_up', 'sleep', 'wake', 'bored',
  'click', 'click_many', 'notice_cursor', 'walk_start', 'walk_arrive', 'feed', 'feed_full',
  'praise_pat', 'level_up', 'topic', 'roam_on', 'roam_off', 'hide', 'recall',
  'pose_front', 'pose_side', 'pose_back', 'first_run', 'shortcut_made',
  'action_hop', 'action_spin', 'action_wave', 'action_dance', 'action_stretch', 'action_lookback', 'action_sit',
  'typing', 'error',
]
for (const cat of required) {
  if (!categories[cat]) { console.error(`  [错误] 代码要用的分类缺失：${cat}`); bad++ }
}
if (bad > 0) {
  console.error(`\n有 ${bad} 个问题，已中止，没有写出 lines.json`)
  process.exit(1)
}

const kw = {}
for (const [cat, words] of Object.entries(keywords)) {
  if (cat.startsWith('_')) continue
  kw[cat] = words
}

const out = {
  version: meta.version || 1,
  defaultName: meta.defaultName,
  persona: meta.persona,
  levels: meta.levels,
  categories,
  chat_keywords: kw,
  chat_fallback: meta.chat_fallback,
}

const target = path.join(content, outName)
fs.writeFileSync(target, JSON.stringify(out, null, 2) + '\n', 'utf8')

const total = Object.values(categories).reduce((n, a) => n + a.length, 0)
console.log(`  OK  ${path.relative(root, target)}`)
console.log(`  分类 ${Object.keys(categories).length} 个，台词 ${total} 条，关键词分类 ${Object.keys(kw).length} 个`)
console.log(`  等级 ${out.levels.map((l) => `${l.lv}:${l.name}(${l.need})`).join(' / ')}`)
