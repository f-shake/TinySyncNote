import type { AIChatMessage, AISettings, AIToolCall } from '../types'

// ── 工具定义（JSON Schema 格式，给 AI 看的） ──
export const AI_TOOLS = [
  {
    type: 'function',
    function: {
      name: 'getNoteContent',
      description: '获取当前笔记的全部 Markdown 内容',
      parameters: { type: 'object', properties: {} }
    }
  },
  {
    type: 'function',
    function: {
      name: 'replaceNoteContent',
      description: '用新内容替换整篇笔记',
      parameters: {
        type: 'object',
        properties: {
          content: { type: 'string', description: '新的 Markdown 内容' }
        },
        required: ['content']
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'insertAtCursor',
      description: '在编辑器光标位置插入文本',
      parameters: {
        type: 'object',
        properties: {
          text: { type: 'string', description: '要插入的文本' }
        },
        required: ['text']
      }
    }
  },
  {
    type: 'function',
    function: {
      name: 'getSelectedText',
      description: '获取当前选中的文本',
      parameters: { type: 'object', properties: {} }
    }
  },
  {
    type: 'function',
    function: {
      name: 'setTitle',
      description: '修改笔记标题',
      parameters: {
        type: 'object',
        properties: {
          title: { type: 'string', description: '新标题' }
        },
        required: ['title']
      }
    }
  }
]

// ── 系统提示词 ──
export const SYSTEM_PROMPT = `# 角色
你是 TinySyncNote 的 AI 笔记助手，帮助用户编辑和管理 Markdown 笔记。

# 能力
你可以读取、修改、扩展和优化用户的笔记内容。你通过工具调用与编辑器交互。

# 工作流程
1. 用户提出需求后，先用 getNoteContent 读取当前内容
2. 分析内容，确定需要做什么修改
3. 使用适当的工具执行修改
4. 向用户解释你做了什么

# 工具使用规则
- 需要了解当前内容时 → getNoteContent
- 需要整体重写、翻译、格式化或大篇幅修改时 → replaceNoteContent（保留整体结构）
- 需要在特定位置添加内容时 → insertAtCursor（先 getNoteContent 确定位置）
- 需要修改标题时 → setTitle
- 用户选中了特定文本提问时 → 先 getSelectedText 获取选中的内容

# 修改规范
- 保持 Markdown 格式有效
- 不要破坏已有的 YAML front matter（如果有的话）
- 不要添加无关的内容
- 保持原笔记的语言，除非用户明确要求翻译
- 列表、代码块、表格等复杂结构要完整保留
- 当用户要求"优化"或"改进"时，保持原意不变，只改进表达

# 输出风格
- 简洁、直接，用中文回复
- 简要说明做了哪些修改
- 除非用户要求，否则不要添加多余的评论或分析`

// ── 工具执行器 ──
export interface EditorActions {
  getNoteContent: () => string
  replaceNoteContent: (content: string) => void
  insertAtCursor: (text: string) => void
  getSelectedText: () => string
  setTitle: (title: string) => void
}

export function executeToolCall(toolCall: { name: string; arguments: string }, editor: EditorActions): string {
  const args = JSON.parse(toolCall.arguments || '{}')
  switch (toolCall.name) {
    case 'getNoteContent': return editor.getNoteContent()
    case 'replaceNoteContent':
      editor.replaceNoteContent(args.content)
      return '笔记内容已替换'
    case 'insertAtCursor':
      editor.insertAtCursor(args.text)
      return `已在光标处插入：${args.text.substring(0, 50)}${args.text.length > 50 ? '...' : ''}`
    case 'getSelectedText': return editor.getSelectedText() || '（未选中任何文本）'
    case 'setTitle':
      editor.setTitle(args.title)
      return `标题已改为：${args.title}`
    default: return `未知工具：${toolCall.name}`
  }
}

// ── 流式调用（用于最终显示，需调用方传 onText 回调） ──
async function callAIStream(
  messages: AIChatMessage[],
  settings: AISettings,
  onText: (chunk: string) => void
): Promise<{ content: string; tool_calls?: AIToolCall[] }> {
  const url = `${settings.ai_url.replace(/\/+$/, '')}/chat/completions`
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${settings.ai_key}` },
    body: JSON.stringify({ model: settings.ai_model || 'gpt-4o-mini', messages, tools: AI_TOOLS, tool_choice: 'auto', stream: true })
  })
  if (!res.ok) throw new Error(`AI 请求失败 (${res.status}): ${await res.text()}`)

  const reader = res.body!.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let content = ''

  // 累加 tool call 参数（流式场景下 arguments 是分块到达的）
  const toolCallAccum: Record<number, { id?: string; name: string; args: string }> = {}

  function flushLine(line: string) {
    if (!line || line === 'data: [DONE]') return
    if (!line.startsWith('data: ')) return
    try {
      const parsed = JSON.parse(line.slice(6))
      const delta = parsed.choices?.[0]?.delta
      if (!delta) return

      // 文本内容
      if (delta.content) {
        content += delta.content
        onText(delta.content)
      }

      // tool calls（分块到达，需要累加 arguments）
      if (delta.tool_calls) {
        for (const tc of delta.tool_calls) {
          if (tc.id) {
            toolCallAccum[tc.index] = { id: tc.id, name: tc.function?.name || '', args: tc.function?.arguments || '' }
          } else if (toolCallAccum[tc.index]) {
            toolCallAccum[tc.index].args += tc.function?.arguments || ''
          }
        }
      }
    } catch { /* skip malformed lines */ }
  }

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() || ''
    for (const line of lines) flushLine(line.trim())
  }
  if (buffer.trim()) flushLine(buffer.trim())

  // 组装 tool calls
  const keys = Object.keys(toolCallAccum)
  const tool_calls: AIToolCall[] | undefined = keys.length > 0
    ? keys.map(k => ({
        id: toolCallAccum[+k].id!,
        type: 'function' as const,
        function: { name: toolCallAccum[+k].name, arguments: toolCallAccum[+k].args }
      }))
    : undefined

  return { content, tool_calls }
}

// ── 完整的 AI 对话循环（流式输出 + 自动多轮 tool call） ──
export async function runAIChat(
  userMessage: string,
  history: AIChatMessage[],
  settings: AISettings,
  editor: EditorActions,
  onText?: (chunk: string) => void  // 可选：流式回调
): Promise<{ reply: string; updatedHistory: AIChatMessage[] }> {
  const messages: AIChatMessage[] = [
    { role: 'system', content: SYSTEM_PROMPT },
    ...history.filter(m => m.role !== 'system'),
    { role: 'user', content: userMessage }
  ]

  let displayContent = ''
  let maxRounds = 5

  while (maxRounds > 0) {
    maxRounds--

    // 使用流式调用
    const result = await callAIStream(messages, settings, (chunk) => {
      if (onText) onText(chunk)
      displayContent += chunk
    })

    // 有 tool call → 执行并继续循环
    if (result.tool_calls && result.tool_calls.length > 0) {
      const reply: AIChatMessage = { role: 'assistant', content: result.content || '', tool_calls: result.tool_calls }
      messages.push(reply)
      for (const tc of result.tool_calls) {
        const r = executeToolCall(tc.function, editor)
        messages.push({ role: 'tool', tool_call_id: tc.id, content: r })
      }
      // 保留已显示的文本（AI 可能在调用工具前已经说了一些话）
      continue
    }

    // 纯文本 → 最终回复
    messages.push({ role: 'assistant', content: result.content })
    return {
      reply: result.content || '已完成',
      updatedHistory: [
        ...history,
        { role: 'user', content: userMessage },
        { role: 'assistant', content: result.content }
      ]
    }
  }

  return {
    reply: displayContent || '已完成',
    updatedHistory: [
      ...history,
      { role: 'user', content: userMessage },
      { role: 'assistant', content: displayContent }
    ]
  }
}
