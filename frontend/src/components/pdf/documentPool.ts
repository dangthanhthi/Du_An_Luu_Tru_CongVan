export function createPdfDocumentPool<T>(load: (url: string) => Promise<{ promise: Promise<T>; destroy: () => Promise<unknown> }>) {
  type Entry = { references: number; task: ReturnType<typeof load>; promise: Promise<T> }
  const mounted = new Map<string, Entry>()

  return { acquire(url: string) {
    let entry = mounted.get(url)

    if (!entry) {
      let task: ReturnType<typeof load>

      try { task = load(url) } catch (error) { task = Promise.reject(error) }
      entry = { references: 0, task, promise: task.then(value => value.promise) }
      mounted.set(url, entry)
    }
    entry.references++
    const owned = entry
    let released = false

    return { promise: owned.promise, release() {
      if (released) return
      released = true
      if (--owned.references !== 0) return
      // No PDF data retained after the last mounted viewer/account boundary.
      if (mounted.get(url) === owned) mounted.delete(url)
      void owned.task.then(value => value.destroy()).catch(() => {})
    } }
  } }
}
