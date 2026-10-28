import { useEffect, useRef } from 'react';

export function Feedback({ tipo, titulo, mensagem, tentar }: {
  tipo: 'idle' | 'loading' | 'empty' | 'error';
  titulo: string;
  mensagem?: string;
  tentar?: () => void;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => { if (tipo === 'error' || tipo === 'empty') heading.current?.focus(); }, [tipo]);
  return <section className={`feedback feedback--${tipo}`} aria-live={tipo === 'error' ? 'assertive' : 'polite'} aria-busy={tipo === 'loading'} data-state={tipo}>
    <span className={tipo === 'loading' ? 'spinner' : 'feedback-symbol'} aria-hidden="true">{tipo === 'loading' ? '' : tipo === 'error' ? '!' : '✓'}</span>
    <h2 ref={heading} tabIndex={-1}>{titulo}</h2>
    {mensagem && <p>{mensagem}</p>}
    {tentar && <button className="button button--primary" onClick={tentar}>Tentar novamente</button>}
  </section>;
}

export function ErrorNotice({ erro }: { erro: string }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => { if (erro) ref.current?.focus(); }, [erro]);
  return erro ? <div className="notice notice--error" role="alert" tabIndex={-1} ref={ref}>{erro}</div> : null;
}
