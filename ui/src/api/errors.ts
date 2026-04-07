/**
 * Reads a failed API response and returns a human-readable error string.
 * Handles RFC 7807 ProblemDetails (both ValidationProblem and plain Problem).
 */
export async function parseApiError(response: Response): Promise<string> {
  const contentType = response.headers.get('content-type') ?? ''

  if (contentType.includes('json')) {
    try {
      const body = await response.json()

      // Flatten field-level validation errors into "Field: message; Field: message"
      const errors: Record<string, string[]> | undefined =
        body.errors ?? body.extensions?.errors

      if (errors && typeof errors === 'object') {
        const messages = Object.entries(errors)
          .flatMap(([field, msgs]) =>
            (msgs as string[]).map(m =>
              field && field !== '' && field.toLowerCase() !== 'general'
                ? `${field}: ${m}`
                : m
            )
          )
        if (messages.length) return messages.join('\n')
      }

      // Single detail / message / error field
      const single =
        body.detail ??
        body.message ??
        body.error ??
        body.title

      if (single && typeof single === 'string') return single
    } catch {
      // JSON parse failed — fall through to status text
    }
  } else {
    // Try plain text (e.g. some 500 responses)
    try {
      const text = await response.text()
      if (text) return text
    } catch {
      // ignore
    }
  }

  return `${response.status} ${response.statusText}`
}
