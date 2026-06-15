import { ref, onMounted, onBeforeUnmount } from 'vue'
import * as signalR from '@microsoft/signalr'
import { getAccessToken } from '../utils/http'

export interface NoteUpdatedEvent {
  noteId: string
  newVersion: number
  action: 'created' | 'updated'
}

export interface NoteDeletedEvent {
  noteId: string
}

export function useSync() {
  const connected = ref(false)
  const lastSyncError = ref<string | null>(null)

  let connection: signalR.HubConnection | null = null
  let noteUpdateHandler: ((evt: NoteUpdatedEvent) => void) | null = null
  let noteDeleteHandler: ((evt: NoteDeletedEvent) => void) | null = null
  let conflictHandler: ((evt: any) => void) | null = null

  function onNoteUpdated(handler: (evt: NoteUpdatedEvent) => void) {
    noteUpdateHandler = handler
  }

  function onNoteDeleted(handler: (evt: NoteDeletedEvent) => void) {
    noteDeleteHandler = handler
  }

  function onConflictDetected(handler: (evt: any) => void) {
    conflictHandler = handler
  }

  async function connect() {
    const token = getAccessToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/sync', {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    // 注册事件处理器
    connection.on('NoteUpdated', (data: NoteUpdatedEvent) => {
      noteUpdateHandler?.(data)
    })

    connection.on('NoteDeleted', (data: NoteDeletedEvent) => {
      noteDeleteHandler?.(data)
    })

    connection.on('ConflictDetected', (data: any) => {
      conflictHandler?.(data)
    })

    // 重连状态
    connection.onreconnecting(() => {
      connected.value = false
      lastSyncError.value = '重新连接中...'
    })

    connection.onreconnected(() => {
      connected.value = true
      lastSyncError.value = null
    })

    connection.onclose(() => {
      connected.value = false
    })

    try {
      await connection.start()
      connected.value = true
      lastSyncError.value = null
    } catch (err: any) {
      lastSyncError.value = err.message || '连接失败'
      connected.value = false
    }
  }

  async function disconnect() {
    if (connection) {
      await connection.stop()
      connection = null
      connected.value = false
    }
  }

  // 自动连接/断开
  onMounted(() => {
    connect()
  })

  onBeforeUnmount(() => {
    disconnect()
  })

  return {
    connected,
    lastSyncError,
    onNoteUpdated,
    onNoteDeleted,
    onConflictDetected,
    connect,
    disconnect
  }
}
