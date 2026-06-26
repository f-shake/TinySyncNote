/**
 * Ctrl+A / Ctrl+Shift+A 递进选择
 * - 表格内：单元格 → 行 → 表 → 全选（沿用 useTableEnhancer 检测）
 * - 正文：  段落 → 最近标题区块 → H2 区块 → H1 区块 → 全选
 * - Ctrl+Shift+A：直接全选
 * - 每次操作弹出提示
 */
import { ElMessage } from 'element-plus'
import type { Ref } from 'vue'

type TableHelpers = {
  getFocusedCell: () => HTMLTableCellElement | null
  getTableSelectionLevel: (table: HTMLTableElement) => number
}

export function useCtrlAEnhancer(
  containerRef: Ref<HTMLElement | null>,
  _getVditor: () => any,
  tableHelpers: TableHelpers
) {
  let ctrlALevel = 0
  let ctrlAContext: 'table' | 'content' | null = null

  // ── 正文区块工具 ──

  /** 收集容器内所有带 data-block 的区块 */
  function getAllBlocks(): Element[] {
    const root = containerRef.value
    return root ? Array.from(root.querySelectorAll('[data-block]')) : []
  }

  /** 当前光标所在的区块（从焦点向上找第一个 [data-block]） */
  function getCurrentBlock(): Element | null {
    const sel = window.getSelection()
    if (!sel?.focusNode) return null
    const root = containerRef.value
    if (!root?.contains(sel.focusNode as Node)) return null
    let el: Node | null = sel.focusNode
    while (el && el !== root) {
      if (el instanceof Element && el.hasAttribute('data-block')) return el
      el = el.parentNode
    }
    return null
  }

  /** 从当前位置找最近的标题（含当前区块本身） */
  function findNearestHeading(currentIdx: number): number | null {
    const blocks = getAllBlocks()
    // 当前区块本身是标题？
    if (/^H[1-6]$/i.test(blocks[currentIdx]?.tagName)) return currentIdx
    // 否则找前面的最近标题
    for (let i = currentIdx - 1; i >= 0; i--) {
      if (/^H[1-6]$/i.test(blocks[i].tagName)) return i
    }
    return null
  }

  /** 从标题位置往后找该区块结束位置（下一个同级或更高级标题之前） */
  function sectionEnd(headingIdx: number): number {
    const blocks = getAllBlocks()
    const level = parseInt(blocks[headingIdx].tagName[1], 10)
    for (let i = headingIdx + 1; i < blocks.length; i++) {
      const m = blocks[i].tagName.match(/^H([1-6])$/i)
      if (m && parseInt(m[1], 10) <= level) return i - 1
    }
    return blocks.length - 1
  }

  /** 从当前位置找级别 <= maxLevel 的标题（含当前区块；级别数字越小越高级） */
  function findHigherHeading(currentIdx: number, maxLevel: number): number | null {
    const blocks = getAllBlocks()
    for (let i = currentIdx; i >= 0; i--) {
      const m = blocks[i]?.tagName.match(/^H([1-6])$/i)
      if (m && parseInt(m[1], 10) <= maxLevel) return i
    }
    return null
  }

  function applyRange(startIdx: number, endIdx: number) {
    const blocks = getAllBlocks()
    if (startIdx < 0 || endIdx >= blocks.length || startIdx > endIdx) return
    const range = document.createRange()
    range.setStart(blocks[startIdx], 0)
    const lastBlock = blocks[endIdx]
    const lastChild = lastBlock.lastChild || lastBlock
    if (lastChild.nodeType === Node.TEXT_NODE) {
      range.setEnd(lastChild, lastChild.textContent?.length || 0)
    } else {
      range.setEndAfter(lastChild)
    }
    const sel = window.getSelection()
    if (sel) { sel.removeAllRanges(); sel.addRange(range) }
  }

  function selectAll() {
    const blocks = getAllBlocks()
    if (blocks.length) applyRange(0, blocks.length - 1)
  }

  // ── 表格处理 ──

  function handleTableCtrlA(e: KeyboardEvent): boolean {
    const cell = tableHelpers.getFocusedCell()
    if (!cell) return false
    const table = cell.closest('table')!
    const level = tableHelpers.getTableSelectionLevel(table)
    if (level >= 3) return false

    e.preventDefault()
    e.stopPropagation()

    const sel = window.getSelection()
    if (!sel) return true

    let r: Range, msg: string

    if (level === 2) {
      r = document.createRange(); r.selectNode(table)
      msg = '已选中整个表格'
    } else if (level === 1) {
      const row = cell.closest('tr')!
      const rc = row.querySelectorAll('td, th')
      r = document.createRange()
      r.setStart(rc[0], 0)
      r.setEnd(rc[rc.length - 1], rc[rc.length - 1].childNodes.length)
      msg = '已选中整行'
    } else {
      r = document.createRange(); r.selectNodeContents(cell)
      msg = '已选中单元格'
    }

    sel.removeAllRanges(); sel.addRange(r)
    ElMessage.info(msg)
    return true
  }

  // ── 正文处理 ──

  /** 执行下一步正文选择，返回是否已处理 */
  function handleContentCtrlA(e: KeyboardEvent): boolean {
    const blocks = getAllBlocks()
    if (!blocks.length) return false
    const currentBlock = getCurrentBlock()
    const currentIdx = currentBlock ? blocks.indexOf(currentBlock) : -1
    if (currentIdx < 0) return false

    e.preventDefault()
    e.stopPropagation()

    let startIdx: number, endIdx: number, msg: string
    const level = ctrlALevel

    if (level >= 3) {
      // 全选
      selectAll()
      ElMessage.info('全选')
      ctrlALevel = 4
      return true
    } else if (level >= 2) {
      // 第 3 步：最近标题的上一级
      const nearest = findNearestHeading(currentIdx)
      if (nearest !== null) {
        const nearestLevel = parseInt(blocks[nearest].tagName[1], 10)
        // 找比最近标题更高级别的标题（level 数字更小）
        const higher = findHigherHeading(nearest, nearestLevel - 1)
        if (higher !== null) {
          startIdx = higher; endIdx = sectionEnd(higher)
          msg = `已选中标题 ${blocks[higher].tagName} 及其内容`
        } else {
          selectAll(); msg = '全选'; ctrlALevel = 3
          ElMessage.info(msg); return true
        }
      } else {
        selectAll(); msg = '全选'; ctrlALevel = 3
        ElMessage.info(msg); return true
      }
    } else if (level >= 1) {
      // 第 2 步：最近标题区块
      const heading = findNearestHeading(currentIdx)
      if (heading !== null) {
        startIdx = heading; endIdx = sectionEnd(heading)
        msg = `已选中标题 ${blocks[heading].tagName} 及其内容`
      } else {
        selectAll(); msg = '全选'; ctrlALevel = 4
        ElMessage.info(msg); return true
      }
    } else {
      // 第 1 步：当前段落
      startIdx = currentIdx; endIdx = currentIdx
      msg = '已选中当前段落'
    }

    applyRange(startIdx!, endIdx!)
    ElMessage.info(msg)
    ctrlALevel++
    return true
  }

  // ── 事件 ──

  function onKeyDown(e: KeyboardEvent) {
    const isCtrl = (e.ctrlKey || e.metaKey)
    if (!isCtrl) return
    const key = e.key.toLowerCase()
    if (key !== 'a') return

    // Ctrl+Shift+A / Cmd+Shift+A → 直接全选
    if (e.shiftKey) {
      e.preventDefault()
      e.stopPropagation()
      selectAll()
      ElMessage.info('全选')
      return
    }

    // Ctrl+A → 判断上下文
    const cell = tableHelpers.getFocusedCell()
    const inTable = cell !== null
    const ctx: 'table' | 'content' = inTable ? 'table' : 'content'

    // 上下文切换时重置
    if (ctx !== ctrlAContext) {
      ctrlALevel = 0
      ctrlAContext = ctx
    }
    // 没有选中内容（折叠选区）→ 新的一次递进
    const sel = window.getSelection()
    if (sel?.rangeCount && sel.getRangeAt(0).collapsed) {
      ctrlALevel = 0
    }

    if (inTable) { handleTableCtrlA(e) } else { handleContentCtrlA(e) }
  }

  function setup() {
    document.addEventListener('keydown', onKeyDown, true)
  }

  function cleanup() {
    document.removeEventListener('keydown', onKeyDown, true)
  }

  return { setup, cleanup }
}
