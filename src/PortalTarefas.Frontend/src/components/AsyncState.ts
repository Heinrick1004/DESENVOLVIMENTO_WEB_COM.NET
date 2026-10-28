import { useCallback, useEffect, useState } from 'react';
import { describeError, isAbort } from '../api/http';

export type AsyncState<T> = { estado: 'idle' } | { estado: 'loading' } | { estado: 'error'; erro: string } | { estado: 'success'; data: T } | { estado: 'empty'; data: T };

/** A chave troca com os parâmetros da rota; a limpeza aborta a chamada anterior. */
export function useAsync<T>(load: (signal: AbortSignal) => Promise<T>, vazia?: (value: T) => boolean) {
  const [state, setState] = useState<AsyncState<T>>({ estado: 'idle' });
  const [tentativa, setTentativa] = useState(0);
  const recarregar = useCallback(() => setTentativa(value => value + 1), []);
  useEffect(() => {
    const controller = new AbortController();
    setState({ estado: 'loading' });
    load(controller.signal).then(data => {
      if (!controller.signal.aborted) setState({ estado: vazia?.(data) ? 'empty' : 'success', data });
    }).catch(error => {
      if (!isAbort(error) && !controller.signal.aborted) setState({ estado: 'error', erro: describeError(error) });
    });
    return () => controller.abort();
  }, [load, vazia, tentativa]);
  return { state, recarregar };
}
