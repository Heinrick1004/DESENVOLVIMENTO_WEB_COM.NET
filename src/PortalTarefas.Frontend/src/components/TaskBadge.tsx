import type { Situacao } from '../api/types';

export const situacoes: Record<Situacao, string> = { Pendente: 'Pendente', EmAndamento: 'Em andamento', Concluida: 'Concluída' };
export const prioridades: Record<number, string> = { 1: 'Baixa', 2: 'Média', 3: 'Alta' };

export function TaskBadge({ situacao }: { situacao: Situacao }) {
  return <span className={`badge badge--${situacao}`}><span aria-hidden="true" />{situacoes[situacao]}</span>;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return 'Sem prazo';
  const date = new Date(value.length === 10 ? `${value}T12:00:00` : value);
  return Number.isNaN(date.valueOf()) ? 'Data indisponível' : new Intl.DateTimeFormat('pt-BR').format(date);
}
