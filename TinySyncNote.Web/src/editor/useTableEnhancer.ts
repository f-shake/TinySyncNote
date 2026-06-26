/**
 * Vditor 表格增强
 * - Tab/Shift+Tab 在表格单元格间导航（WYSIWYG / IR 模式）
 * - 右键自定义菜单：插入/删除行或列
 * - Ctrl+A 递进选择（单元格→行→表）
 * - 选中单元格高亮
 * - Backspace 删除行/表
 */
import type { Ref } from 'vue'

export function useTableEnhancer(
  containerRef: Ref<HTMLElement | null>,
  getVditor: () => any
) {
  let menuEl: HTMLDivElement | null = null
  let contextCell: HTMLTableCellElement | null = null

  // ── 工具函数 ──

  /** 判断节点是否在表格内 */
  function isInsideTable(el: Node | null): boolean {
    while (el) {
      const table = el instanceof HTMLTableCellElement
      if (table) return true
      el = el.parentNode
    }
    return false
  }

  /** 获取光标所在的表格单元格 */
  function getFocusedCell(): HTMLTableCellElement | null {
    const sel = window.getSelection()
    if (!sel?.focusNode) return null
    return sel.focusNode instanceof HTMLTableCellElement
      ? sel.focusNode
      : sel.focusNode.parentElement?.closest('td, th') ?? null
  }

  /** 获取单元格在表格中的位置信息 */
  function getCellInfo(cell: HTMLTableCellElement) {
    const row = cell.closest('tr')!
    const table = cell.closest('table')!
    const rows = Array.from(table.querySelectorAll('tr'))
    const rowIndex = rows.indexOf(row)
    const cells = Array.from(row.cells)
    const colIndex = cells.indexOf(cell)
    return { table, row, rows, rowIndex, colIndex }
  }

  /** 将光标移到指定单元格 */
  function moveCursorToCell(table: HTMLTableElement, rowIndex: number, colIndex: number) {
    const rows = table.querySelectorAll('tr')
    if (rowIndex < 0 || rowIndex >= rows.length) return
    const cells = rows[rowIndex].querySelectorAll('td, th')
    if (colIndex < 0 || colIndex >= cells.length) return
    const cell = cells[colIndex] as HTMLTableCellElement
    cell.focus()
    const range = document.createRange()
    range.selectNodeContents(cell)
    range.collapse(true)
    const sel = window.getSelection()
    if (sel) {
      sel.removeAllRanges()
      sel.addRange(range)
    }
  }

  /** 在 contenteditable 上派发 input 事件，通知 Vditor 内容已变更 */
  function notifyContentChange() {
    const vd = getVditor()
    if (!vd) return
    const mode = vd.getCurrentMode?.() || 'wysiwyg'
    const selectors: Record<string, string> = {
      wysiwyg: '.vditor-content',
      ir: '.vditor-ir',
      sv: '.vditor-sv textarea',
    }
    const el = containerRef.value?.querySelector(selectors[mode] || '.vditor-content')
    if (el) {
      el.dispatchEvent(new Event('input', { bubbles: true }))
    }
  }

  /** 检测当前选区覆盖情况 0=无选区，1=单元格级（选了一些内容但未占满整行），2=行级（已占满整行），3=表级（已占满全表） */
  function getTableSelectionLevel(table: HTMLTableElement): number {
    const sel = window.getSelection()
    if (!sel || sel.rangeCount === 0) return 0
    const range = sel.getRangeAt(0)

    if (range.collapsed) return 0

    const allCells = Array.from(table.querySelectorAll<HTMLTableCellElement>('td, th'))
    if (allCells.length === 0) return 0

    const touched = new Set<HTMLTableCellElement>()
    const touchedRows = new Set<HTMLTableRowElement>()
    for (const cell of allCells) {
      if (range.intersectsNode(cell)) {
        touched.add(cell)
        touchedRows.add(cell.closest('tr') as HTMLTableRowElement)
      }
    }

    if (touched.size > 0) {
      const tableNode: Node = table
      if (!tableNode.contains(range.startContainer) || !tableNode.contains(range.endContainer)) {
        return 3
      }
    }

    const totalCells = allCells.length

    if (touched.size >= totalCells) return 3
    if (touchedRows.size > 1) return 2
    if (touchedRows.size === 1 && touched.size > 0) {
      const row = touchedRows.values().next().value!
      const rowCells = row.querySelectorAll('td, th')
      if (touched.size >= rowCells.length) return 2
      return 1
    }

    return 0
  }

  /** 根据选区获取被覆盖的表格元素（供外部使用，如剪贴板处理） */
  function findTableFromSelection(): HTMLTableElement | null {
    const sel = window.getSelection()
    if (!sel || !sel.rangeCount) return null
    const range = sel.getRangeAt(0)
    const cell = getFocusedCell()
    if (cell) return cell.closest('table')
    let node: Node | null = range.commonAncestorContainer
    let table = node instanceof HTMLTableElement ? node
              : node?.parentElement?.closest('table') ?? null
    if (table) return table
    for (const tbl of containerRef.value?.querySelectorAll('table') ?? []) {
      if (range.intersectsNode(tbl)) return tbl
    }
    return null
  }

  // ── Tab 导航 ──

  function handleTabKey(e: KeyboardEvent) {
    const vd = getVditor()
    if (!vd) return

    const mode = vd.getCurrentMode?.() || 'wysiwyg'
    if (mode === 'sv') return

    const cell = getFocusedCell()
    if (!cell) return

    e.preventDefault()
    e.stopPropagation()

    const info = getCellInfo(cell)
    const { table, rowIndex, colIndex, rows } = info
    const cells = rows[rowIndex].querySelectorAll('td, th')

    if (e.shiftKey) {
      if (colIndex > 0) {
        moveCursorToCell(table, rowIndex, colIndex - 1)
      } else if (rowIndex > 0) {
        const prevCells = rows[rowIndex - 1].querySelectorAll('td, th')
        moveCursorToCell(table, rowIndex - 1, prevCells.length - 1)
      }
    } else {
      if (colIndex < cells.length - 1) {
        moveCursorToCell(table, rowIndex, colIndex + 1)
      } else if (rowIndex < rows.length - 1) {
        moveCursorToCell(table, rowIndex + 1, 0)
      } else {
        const newRow = table.insertRow()
        const cellCount = cells.length
        for (let i = 0; i < cellCount; i++) {
          const td = newRow.insertCell()
          td.innerHTML = '<br>'
        }
        moveCursorToCell(table, rowIndex + 1, 0)
      }
    }

    notifyContentChange()
  }

  // ── 右键菜单 ──

  const MENU_ITEMS = [
    { label: '在上方插入行', action: 'insertRowAbove' },
    { label: '在下方插入行', action: 'insertRowBelow' },
    { divider: true },
    { label: '在左侧插入列', action: 'insertColLeft' },
    { label: '在右侧插入列', action: 'insertColRight' },
    { divider: true },
    { label: '左对齐', action: 'alignLeft' },
    { label: '中对齐', action: 'alignCenter' },
    { label: '右对齐', action: 'alignRight' },
    { divider: true },
    { label: '删除行', action: 'deleteRow' },
    { label: '删除列', action: 'deleteCol' },
    { label: '删除表格', action: 'deleteTable' },
  ] as const

  function createMenu() {
    if (menuEl) return
    menuEl = document.createElement('div')
    menuEl.className = 'vditor-table-menu'
    menuEl.setAttribute('style',
      'display:none;position:fixed;z-index:99999;' +
      'min-width:170px;padding:4px 0;font-size:13px;' +
      'user-select:none;'
    )

    for (const item of MENU_ITEMS) {
      if ('divider' in item) {
        const divider = document.createElement('div')
        divider.className = 'vditor-table-menu-divider'
        menuEl.appendChild(divider)
      } else {
        const btn = document.createElement('div')
        btn.className = 'vditor-table-menu-item'
        btn.textContent = item.label
        btn.dataset.action = item.action
        btn.addEventListener('mouseenter', () => btn.classList.add('hover'))
        btn.addEventListener('mouseleave', () => btn.classList.remove('hover'))
        btn.addEventListener('click', () => {
          executeAction(item.action)
          hideMenu()
        })
        menuEl.appendChild(btn)
      }
    }

    document.body.appendChild(menuEl)
  }

  function showMenu(e: MouseEvent) {
    const target = e.target as Node
    const cell = target instanceof HTMLTableCellElement
      ? target
      : (target.parentElement?.closest('td, th') as HTMLTableCellElement | null) ?? null
    if (!cell) return

    e.preventDefault()
    contextCell = cell
    createMenu()
    if (!menuEl) return

    menuEl.style.display = 'block'
    const menuW = menuEl.offsetWidth
    const menuH = menuEl.offsetHeight

    let x = e.clientX
    let y = e.clientY
    if (x + menuW > window.innerWidth) x = window.innerWidth - menuW - 8
    if (y + menuH > window.innerHeight) y = window.innerHeight - menuH - 8
    menuEl.style.left = x + 'px'
    menuEl.style.top = y + 'px'
  }

  function hideMenu() {
    if (menuEl) menuEl.style.display = 'none'
    contextCell = null
  }

  function executeAction(action: string) {
    if (!contextCell) return
    const info = getCellInfo(contextCell)
    const { table, rowIndex, colIndex, rows } = info

    const isInThead = rows[rowIndex].closest('thead') !== null
    const createCell = (asTh: boolean) => {
      const cell = document.createElement(asTh ? 'th' : 'td')
      cell.innerHTML = '<br>'
      return cell
    }

    switch (action) {
      case 'insertRowAbove': {
        const curRow = rows[rowIndex]
        const cellCount = curRow.cells.length
        if (isInThead) {
          const newRow = document.createElement('tr')
          for (let i = 0; i < cellCount; i++) newRow.appendChild(createCell(true))
          curRow.parentNode?.insertBefore(newRow, curRow)

          const tbody = table.querySelector('tbody') || table.createTBody()
          const movedRow = document.createElement('tr')
          for (let i = 0; i < cellCount; i++) {
            const td = document.createElement('td')
            td.innerHTML = curRow.cells[i].innerHTML || '<br>'
            movedRow.appendChild(td)
          }
          curRow.remove()
          tbody.insertBefore(movedRow, tbody.firstChild)
        } else {
          const newRow = document.createElement('tr')
          for (let i = 0; i < cellCount; i++) newRow.appendChild(createCell(false))
          curRow.parentNode?.insertBefore(newRow, curRow)
        }
        break
      }
      case 'insertRowBelow': {
        const curRow = rows[rowIndex]
        const cellCount = curRow.cells.length
        if (isInThead) {
          const tbody = table.querySelector('tbody') || table.createTBody()
          const newRow = document.createElement('tr')
          for (let i = 0; i < cellCount; i++) newRow.appendChild(createCell(false))
          tbody.insertBefore(newRow, tbody.firstChild)
        } else {
          const newRow = document.createElement('tr')
          for (let i = 0; i < cellCount; i++) newRow.appendChild(createCell(false))
          curRow.parentNode?.insertBefore(newRow, curRow.nextSibling)
        }
        break
      }
      case 'insertColLeft': {
        for (const row of rows) {
          const isTh = row.closest('thead') !== null
          const cell = createCell(isTh)
          row.insertBefore(cell, row.cells[colIndex] ?? null)
        }
        break
      }
      case 'insertColRight': {
        for (const row of rows) {
          const isTh = row.closest('thead') !== null
          const cell = createCell(isTh)
          row.insertBefore(cell, row.cells[colIndex + 1] ?? null)
        }
        break
      }
      case 'deleteRow': {
        if (rows.length <= 1) {
          table.parentNode?.removeChild(table)
        } else {
          rows[rowIndex].parentNode?.removeChild(rows[rowIndex])
        }
        break
      }
      case 'deleteCol': {
        const cellCount = rows[0]?.cells.length ?? 0
        if (cellCount <= 1) {
          table.parentNode?.removeChild(table)
        } else {
          for (const row of rows) {
            const cells = row.querySelectorAll('td, th')
            if (cells[colIndex]) cells[colIndex].remove()
          }
        }
        break
      }
      case 'deleteTable': {
        table.parentNode?.removeChild(table)
        break
      }
      case 'alignLeft':
      case 'alignCenter':
      case 'alignRight': {
        const align = action === 'alignLeft' ? 'left'
                    : action === 'alignCenter' ? 'center'
                    : 'right'

        const targetCols = new Set<number>()
        const sel = window.getSelection()
        if (sel?.rangeCount) {
          const range = sel.getRangeAt(0)
          for (const row of rows) {
            const rowCells = row.querySelectorAll('td, th')
            for (let i = 0; i < rowCells.length; i++) {
              if (range.intersectsNode(rowCells[i])) targetCols.add(i)
            }
          }
        }
        if (targetCols.size === 0) targetCols.add(colIndex)

        for (const row of rows) {
          const cells = row.querySelectorAll('td, th')
          for (const ci of targetCols) {
            if (cells[ci]) (cells[ci] as HTMLElement).style.textAlign = align
          }
        }
        break
      }
    }

    notifyContentChange()
  }

  // ── 按键常量 ──
  const KEY = { TAB: 'Tab', ESC: 'Escape', BACKSPACE: 'Backspace' }

  // ── 事件监听 ──

  function onKeyDown(e: KeyboardEvent) {
    if (e.key === KEY.TAB) {
      handleTabKey(e)
    }
    if (e.key === KEY.ESC) {
      hideMenu()
    }
    if (e.key === KEY.BACKSPACE) {
      const table = findTableFromSelection()
      if (!table) return
      // 如果选区内有非表格内容，不拦截 → 浏览器默认行为删除整个选区
      const sel = window.getSelection()
      if (sel?.rangeCount) {
        const range = sel.getRangeAt(0)
        const tblNode: Node = table
        if (!tblNode.contains(range.startContainer) || !tblNode.contains(range.endContainer)) return
      }
      const level = getTableSelectionLevel(table)
      if (level < 2) return
      e.preventDefault()
      e.stopPropagation()
      if (level >= 3) {
        table.parentNode?.removeChild(table)
      } else {
        const cell = getFocusedCell()
        if (cell) {
          const row = cell.closest('tr')!
          if (table.querySelectorAll('tr').length <= 1) {
            table.parentNode?.removeChild(table)
          } else {
            row.parentNode?.removeChild(row)
          }
        }
      }
      notifyContentChange()
    }
  }

  function onContextMenu(e: MouseEvent) {
    if (!containerRef.value?.contains(e.target as Node)) return
    if (isInsideTable(e.target as Node)) {
      e.preventDefault()
      showMenu(e)
    }
  }

  function onMouseDown(e: MouseEvent) {
    if (menuEl && !menuEl.contains(e.target as Node)) {
      hideMenu()
    }
  }

  // ── 选中单元格高亮 ──

  let _highlightTimer: ReturnType<typeof setTimeout> | null = null
  const SELECTED_CLASS = 'tsn-cell-selected'

  function onSelectionChange() {
    if (_highlightTimer) clearTimeout(_highlightTimer)
    _highlightTimer = setTimeout(() => {
      if (containerRef.value) {
        containerRef.value.querySelectorAll(`.${SELECTED_CLASS}`).forEach(el =>
          el.classList.remove(SELECTED_CLASS))
      }
      const sel = window.getSelection()
      if (!sel || !sel.rangeCount || !containerRef.value?.contains(sel.focusNode as Node)) return
      const range = sel.getRangeAt(0)
      if (range.collapsed) return

      const allTables = containerRef.value?.querySelectorAll('table') ?? []
      for (const table of allTables) {
        if (!containerRef.value?.contains(table)) continue
        if (!range.intersectsNode(table)) continue
        for (const c of table.querySelectorAll<HTMLTableCellElement>('td, th')) {
          if (range.intersectsNode(c)) c.classList.add(SELECTED_CLASS)
        }
      }
    }, 30)
  }

  // ── 公开 API ──

  function setup() {
    document.addEventListener('keydown', onKeyDown, true)
    document.addEventListener('contextmenu', onContextMenu)
    document.addEventListener('mousedown', onMouseDown)
    document.addEventListener('selectionchange', onSelectionChange)
  }

  function cleanup() {
    document.removeEventListener('keydown', onKeyDown, true)
    document.removeEventListener('contextmenu', onContextMenu)
    document.removeEventListener('mousedown', onMouseDown)
    document.removeEventListener('selectionchange', onSelectionChange)
    if (_highlightTimer) clearTimeout(_highlightTimer)
    hideMenu()
    menuEl?.remove()
    menuEl = null
  }

  return { setup, cleanup, findTableFromSelection, getTableSelectionLevel, getFocusedCell }
}
