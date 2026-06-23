/**
 * Vditor 表格增强
 * - Tab/Shift+Tab 在表格单元格间导航（WYSIWYG / IR 模式）
 * - 右键自定义菜单：插入/删除行或列
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
    // Vditor 在不同模式下 contenteditable 元素的选择器不同
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

    // 折叠选区（只有光标，没有选中内容）→ 无选区
    if (range.collapsed) return 0

    const allCells = Array.from(table.querySelectorAll<HTMLTableCellElement>('td, th'))
    if (allCells.length === 0) return 0

    // 收集选区覆盖到的单元格
    const touched = new Set<HTMLTableCellElement>()
    const touchedRows = new Set<HTMLTableRowElement>()
    for (const cell of allCells) {
      if (range.intersectsNode(cell)) {
        touched.add(cell)
        touchedRows.add(cell.closest('tr') as HTMLTableRowElement)
      }
    }

    // 选区混合：一部分在表格内，一部分在表格外 → 直接全选
    if (touched.size > 0) {
      const tableNode: Node = table
      if (!tableNode.contains(range.startContainer) || !tableNode.contains(range.endContainer)) {
        return 3
      }
    }

    const totalCells = allCells.length

    if (touched.size >= totalCells) return 3          // 覆盖了全部单元格 → 表级
    if (touchedRows.size > 1) return 2                 // 跨行 → 行级已满，跳到表级
    if (touchedRows.size === 1 && touched.size > 0) {
      const row = touchedRows.values().next().value!
      const rowCells = row.querySelectorAll('td, th')
      if (touched.size >= rowCells.length) return 2   // 覆盖了整行 → 行级
      return 1                                         // 单行内部分内容 → 单元格级
    }

    return 0
  }

  // ── Tab 导航 ──

  function handleTabKey(e: KeyboardEvent) {
    const vd = getVditor()
    if (!vd) return

    // SV 模式下不拦截 Tab
    const mode = vd.getCurrentMode?.() || 'wysiwyg'
    if (mode === 'sv') return

    const cell = getFocusedCell()
    if (!cell) return // 不在表格内

    e.preventDefault()
    e.stopPropagation()

    const info = getCellInfo(cell)
    const { table, rowIndex, colIndex, rows } = info
    const cells = rows[rowIndex].querySelectorAll('td, th')

    if (e.shiftKey) {
      // Shift+Tab：前一个单元格
      if (colIndex > 0) {
        moveCursorToCell(table, rowIndex, colIndex - 1)
      } else if (rowIndex > 0) {
        const prevCells = rows[rowIndex - 1].querySelectorAll('td, th')
        moveCursorToCell(table, rowIndex - 1, prevCells.length - 1)
      }
    } else {
      // Tab：下一个单元格
      if (colIndex < cells.length - 1) {
        moveCursorToCell(table, rowIndex, colIndex + 1)
      } else if (rowIndex < rows.length - 1) {
        moveCursorToCell(table, rowIndex + 1, 0)
      } else {
        // 最后一个单元格 → 新增一行
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
    // 基本样式用 class 控制（配合暗色模式），内联样式作为兜底
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

    // 先显示才能测出真实高度
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
          // 新行成为表头，旧表头移到 tbody 顶部并转为 td
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
          // 在表头下方插入 → 插入一条表体行到 tbody 顶部
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

        // 确定受影响列：如果有选区则应用于所有被选中的列
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
        // 无选区时退回到右键点击的列
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
  const KEY = { TAB: 'Tab', ESC: 'Escape', A: 'a', BACKSPACE: 'Backspace', DELETE: 'Delete' }

  // ── 事件监听 ──

  function onKeyDown(e: KeyboardEvent) {
    if (e.key === KEY.TAB) {
      handleTabKey(e)
    }
    if (e.key === KEY.ESC) {
      hideMenu()
    }
    // Ctrl+A / Cmd+A 递进选择：单元格 → 整行 → 整个表格 → 全选
    if ((e.ctrlKey || e.metaKey) && e.key === KEY.A) {
      const cell = getFocusedCell()
      if (cell) {
        const table = cell.closest('table')!
        const level = getTableSelectionLevel(table)
        const sel = window.getSelection()
        if (!sel) return

        // Level 3+：已经是全表 → 放行给默认行为（全选）
        if (level >= 3) return

        e.preventDefault()
        e.stopPropagation()

        let targetRange: Range

        if (level === 2) {
          // 已选中整行 → 选中整个表格（selectNode 包含 <table> 标签，复制保留结构）
          targetRange = document.createRange()
          targetRange.selectNode(table)
        } else if (level === 1) {
          // 已选中单元格 → 选中整行
          const row = cell.closest('tr')!
          const rowCells = row.querySelectorAll('td, th')
          targetRange = document.createRange()
          targetRange.setStart(rowCells[0], 0)
          targetRange.setEnd(rowCells[rowCells.length - 1], rowCells[rowCells.length - 1].childNodes.length)
        } else {
          // 未选中/部分选中 → 选中当前单元格内容
          targetRange = document.createRange()
          targetRange.selectNodeContents(cell)
        }

        sel.removeAllRanges()
        sel.addRange(targetRange)
      }
    }
    // Backspace：选中整行→删行，选中全表→删表；Delete始终清空内容
    if (e.key === KEY.BACKSPACE) {
      const table = findTableFromSelection()
      if (!table) return
      const level = getTableSelectionLevel(table)
      if (level < 2) return // 单元格级 → 默认行为（清空内容）
      e.preventDefault()
      e.stopPropagation()
      if (level >= 3) {
        // 全表 → 删表
        table.parentNode?.removeChild(table)
      } else {
        // 整行 → 删行
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

  function findTableFromSelection(): HTMLTableElement | null {
    const sel = window.getSelection()
    if (!sel || !sel.rangeCount) return null
    const range = sel.getRangeAt(0)
    // 从 focusNode 找
    const cell = getFocusedCell()
    if (cell) return cell.closest('table')
    // 从 commonAncestorContainer 找
    let node: Node | null = range.commonAncestorContainer
    let table = node instanceof HTMLTableElement ? node
              : node?.parentElement?.closest('table') ?? null
    if (table) return table
    // 遍历编辑器内所有表格
    for (const tbl of containerRef.value?.querySelectorAll('table') ?? []) {
      if (range.intersectsNode(tbl)) return tbl
    }
    return null
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

  /** 复制拦截：当选区在表格内时，构造只含选中单元格的 <table> HTML */
  function onCopy(e: ClipboardEvent) {
    // 只拦截编辑器内的复制
    if (!containerRef.value?.contains(e.target as Node)) return
    const fragment = buildTableFromSelection()
    if (!fragment) return
    e.preventDefault()
    e.stopPropagation()
    const html = fragment.outerHTML
    e.clipboardData?.setData('text/html', html)
    e.clipboardData?.setData('text/plain', fragment.textContent || '')
  }

  /** 根据当前选区，构造包含所选单元格的 <table> 元素（完整选区返回原表，部分选区只取选中的行列） */
  function buildTableFromSelection(): HTMLTableElement | null {
    const sel = window.getSelection()
    if (!sel || !sel.rangeCount) return null
    const range = sel.getRangeAt(0)

    // 找到选区所在的表格
    let node: Node | null = range.commonAncestorContainer
    let table = node instanceof HTMLTableElement ? node
              : node?.parentElement?.closest('table') ?? null
    if (!table) return null

    const allRows = Array.from(table.querySelectorAll('tr'))
    const allCells = Array.from(table.querySelectorAll<HTMLTableCellElement>('td, th'))

    // 找出被选区覆盖的单元格
    const touched = new Set<HTMLTableCellElement>()
    for (const cell of allCells) {
      if (range.intersectsNode(cell)) touched.add(cell)
    }
    if (touched.size === 0) return null

    // 全选 → 直接返回原表
    if (touched.size >= allCells.length) return table.cloneNode(true) as HTMLTableElement

    // 部分选中 → 构造新表
    const out = document.createElement('table')
    const outTbody = document.createElement('tbody')
    out.appendChild(outTbody)

    for (const row of allRows) {
      const rowCells = Array.from(row.querySelectorAll<HTMLTableCellElement>('td, th'))
      const kept = rowCells.filter(c => touched.has(c))
      if (kept.length === 0) continue // 该行无选中单元格
      const outTr = document.createElement('tr')
      for (const cell of kept) {
        const tag = cell.tagName === 'TH' ? 'th' : 'td'
        const newCell = document.createElement(tag)
        newCell.innerHTML = cell.innerHTML
        // 复制文本对齐样式
        if (cell.matches('[style*="text-align"]')) {
          newCell.style.textAlign = cell.style.textAlign
        }
        outTr.appendChild(newCell)
      }
      outTbody.appendChild(outTr)
    }

    return out
  }

  // ── 选中单元格高亮 ──

  let _highlightTimer: ReturnType<typeof setTimeout> | null = null
  const SELECTED_CLASS = 'tsn-cell-selected'

  function onSelectionChange() {
    if (_highlightTimer) clearTimeout(_highlightTimer)
    _highlightTimer = setTimeout(() => {
      // 先清除所有高亮
      if (containerRef.value) {
        containerRef.value.querySelectorAll(`.${SELECTED_CLASS}`).forEach(el =>
          el.classList.remove(SELECTED_CLASS))
      }
      const sel = window.getSelection()
      if (!sel || !sel.rangeCount || !containerRef.value?.contains(sel.focusNode as Node)) return
      const range = sel.getRangeAt(0)
      if (range.collapsed) return

      // 从 focusNode 或 commonAncestorContainer 找到选区涉及的表格
      let table: HTMLTableElement | null = null
      const cell = sel.focusNode instanceof HTMLTableCellElement
        ? sel.focusNode
        : sel.focusNode?.parentElement?.closest('td, th')
      if (cell) {
        table = cell.closest('table')
      } else {
        // selectNode(table) 选中整个表格时，focusNode 不在单元格内
        // 直接检查编辑器中哪些表格被选区覆盖
        const allTables = containerRef.value?.querySelectorAll('table') ?? []
        for (const tbl of allTables) {
          if (range.intersectsNode(tbl)) { table = tbl; break }
        }
      }
      if (!table || !containerRef.value?.contains(table)) return

      for (const c of table.querySelectorAll<HTMLTableCellElement>('td, th')) {
        if (range.intersectsNode(c)) c.classList.add(SELECTED_CLASS)
      }
    }, 30)
  }

  // ── 公开 API ──

  function setup() {
    // 使用捕获阶段拦截 Tab，确保在 Vditor 内部处理器之前触发
    document.addEventListener('keydown', onKeyDown, true)
    document.addEventListener('contextmenu', onContextMenu)
    document.addEventListener('mousedown', onMouseDown)
    // copy 不冒泡，用 capture 确保在 Vditor 之前拿到事件
    document.addEventListener('copy', onCopy, true)
    document.addEventListener('selectionchange', onSelectionChange)
  }

  function cleanup() {
    document.removeEventListener('keydown', onKeyDown, true)
    document.removeEventListener('contextmenu', onContextMenu)
    document.removeEventListener('mousedown', onMouseDown)
    document.removeEventListener('copy', onCopy, true)
    document.removeEventListener('selectionchange', onSelectionChange)
    if (_highlightTimer) clearTimeout(_highlightTimer)
    hideMenu()
    menuEl?.remove()
    menuEl = null
  }

  return { setup, cleanup }
}
