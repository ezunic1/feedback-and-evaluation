export type ProblemErrors = Record<string, string[]>;

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: ProblemErrors;
  traceId?: string;
  [k: string]: any;
}

function isProblemDetailsLike(x: any): x is ProblemDetails {
  return !!x && typeof x === 'object' && (
    typeof x.detail === 'string' ||
    typeof x.title === 'string' ||
    (x.errors && typeof x.errors === 'object')
  );
}

export function extractProblem(err: any): ProblemDetails {
  if (isProblemDetailsLike(err)) return err;

  const payload = err?.error;

  if (isProblemDetailsLike(payload)) {
    return {
      type: payload.type,
      title: payload.title,
      status: typeof payload.status === 'number' ? payload.status : err?.status,
      detail: payload.detail,
      instance: payload.instance,
      errors: payload.errors,
      traceId: payload.traceId ?? err?.headers?.get?.('x-trace-id') ?? err?.traceId,
      ...payload
    };
  }

  if (typeof payload === 'string') {
    return {
      status: err?.status ?? 0,
      title: 'Error',
      detail: payload
    };
  }

  if (typeof err?.message === 'string' && !err?.status) {
    return { title: 'Error', detail: err.message };
  }

  return {
    status: err?.status ?? 0,
    title: 'Error',
    detail: 'Request failed'
  };
}
