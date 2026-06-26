/**
 * 剪贴板增强：编辑器内复制时同时输出 HTML（富文本）和 Markdown（纯文本）
 * - 表格选区：构造独立 <table> HTML（含边框样式）
 * - 非表格选区：清理 Vditor 内部标记后输出
 */
import type { Ref } from 'vue'

export function useClipboardEnhancer(
  containerRef: Ref<HTMLElement | null>,
  getVditor: () => any
) {
  /** 给表格加上内联边框样式（剪贴板 HTML 不含外部样式表） */
  function styleTableForClipboard(table: HTMLTableElement) {
    table.setAttribute('style', 'border-collapse:collapse; border:1px solid #bbb;')
    for (const cell of table.querySelectorAll<HTMLElement>('td, th')) {
      cell.style.border = '1px solid #bbb'
      cell.style.padding = '4px 8px'
    }
  }

  /** 根据当前选区，构造包含所选单元格的 <table> 元素 */
  function buildTableFromSelection(): HTMLTableElement | null {
    const sel = window.getSelection()
    if (!sel || !sel.rangeCount) return null
    const range = sel.getRangeAt(0)

    let node: Node | null = range.commonAncestorContainer
    const table = node instanceof HTMLTableElement ? node
                : node?.parentElement?.closest('table') ?? null
    if (!table) return null

    const allCells = Array.from(table.querySelectorAll<HTMLTableCellElement>('td, th'))
    const touched = new Set<HTMLTableCellElement>()
    for (const cell of allCells) {
      if (range.intersectsNode(cell)) touched.add(cell)
    }
    if (touched.size === 0) return null

    if (touched.size >= allCells.length) return table.cloneNode(true) as HTMLTableElement

    const out = document.createElement('table')
    const outTbody = document.createElement('tbody')
    out.appendChild(outTbody)

    const allRows = Array.from(table.querySelectorAll('tr'))
    for (const row of allRows) {
      const rowCells = Array.from(row.querySelectorAll<HTMLTableCellElement>('td, th'))
      const kept = rowCells.filter(c => touched.has(c))
      if (kept.length === 0) continue
      const outTr = document.createElement('tr')
      for (const cell of kept) {
        const tag = cell.tagName === 'TH' ? 'th' : 'td'
        const newCell = document.createElement(tag)
        newCell.innerHTML = cell.innerHTML
        if (cell.matches('[style*="text-align"]')) {
          newCell.style.textAlign = cell.style.textAlign
        }
        outTr.appendChild(newCell)
      }
      outTbody.appendChild(outTr)
    }

    return out
  }

  /** 复制拦截：剪贴板同时输出 HTML（Word 等用）和纯文本（Markdown） */
  function onCopy(e: ClipboardEvent) {
    if (!containerRef.value?.contains(e.target as Node)) return
    const sel = window.getSelection()
    if (!sel || !sel.rangeCount) return
    const range = sel.getRangeAt(0)
    if (range.collapsed) return

    e.preventDefault()
    e.stopPropagation()

    // 纯文本格式 → 选区文本（Markdown）
    const vd = getVditor()
    const fullMd = vd?.getValue() || ''
    const selectedText = sel.toString()
    let md = selectedText
    if (selectedText && fullMd.includes(selectedText)) {
      md = selectedText
    }
    e.clipboardData?.setData('text/plain', md)

    // HTML 格式 → 构造干净渲染 HTML
    const div = document.createElement('div')
    div.appendChild(range.cloneContents())
    div.querySelectorAll('.vditor-ir__marker, .vditor-wysiwyg__marker').forEach(el => el.remove())
    div.querySelectorAll('[data-block], [data-type]').forEach(el => {
      el.removeAttribute('data-block')
      el.removeAttribute('data-type')
    })

    const tableFragment = buildTableFromSelection()
    if (tableFragment) {
      styleTableForClipboard(tableFragment)
      e.clipboardData?.setData('text/html', tableFragment.outerHTML)
      return
    }
    e.clipboardData?.setData('text/html', div.innerHTML)
  }

  function setup() {
    document.addEventListener('copy', onCopy, true)
  }

  function cleanup() {
    document.removeEventListener('copy', onCopy, true)
  }

  return { setup, cleanup }
}
