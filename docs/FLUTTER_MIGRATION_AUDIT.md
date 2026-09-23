# Auditoria final da migração Flutter — 2026-09-23

## Resultado

A migração do cliente web para Flutter foi encerrada. O cliente oficial está em `flutter_app/`; o antigo `frontend/` em HTML/CSS/JS foi removido depois desta comparação.

## Paridade funcional verificada

| Área | Flutter | Observação |
|---|---|---|
| Login adulto | ✅ | E-mail/senha, recuperação e criação de família |
| Login de membro | ✅ | Código da família, seleção de perfil e PIN |
| Rotina de hoje | ✅ | Concluir/reabrir, criar, editar, remover e reordenar |
| Formulário completo de tarefa | ✅ | Descrição, pontos, tipo, período, opções e motivo |
| Promover tarefa avulsa | ✅ | Adulto pode transformar tarefa do dia em permanente |
| Recorrência rápida | ✅ | Diária, dia sim/dia não e dias específicos |
| Escolha dentro da tarefa | ✅ | Opções selecionáveis antes da conclusão |
| Autonomia | ✅ | Iniciativa, motivo de não realização e planejamento da noite |
| “E agora?” | ✅ | Sugere a próxima tarefa, respeitando o planejamento da noite |
| Feedback de esforço | ✅ | Mensagem contextual após conclusão |
| Timer de jogo | ✅ | Iniciar, pausar/retomar localmente, consumir, ajustar e alarme 3x |
| Virada do dia | ✅ | Recarrega a rotina na mudança de data e ao retomar o app |
| Carregamento lento | ✅ | Login e primeira carga de Hoje avisam após 4 s |
| Badges | ✅ | Pendências de Hoje para membro e aprovações da Loja para adulto |
| Reação do adulto | ✅ | Envio e visualização da reação do dia |
| Amanhã | ✅ | Planejamento, sugestões e rotina/autonomia |
| Chat | ✅ | Chat familiar autenticado com histórico |
| Histórico | ✅ | Paginação e detalhe por dia |
| Pontos | ✅ | Saldo, extrato, ajuste adulto e resumo de autonomia |
| Loja | ✅ | Itens, resgate, aprovação/rejeição, editar e ativar/desativar |
| PACUS | ✅ | Estágio, tamanho, histórico e ajuste adulto |
| Configurações | ✅ | Família, PIN, fuso, timer, valor do PP, estágios e tarefas permanentes |
| Privacidade/Termos | ✅ | Páginas estáticas dentro do bundle web Flutter |
| Tema | ✅ | Claro, escuro e sistema |

## Itens do legado não migrados por não serem funcionalidade ativa

- wrappers de API de pausa/retomada do timer que não eram usados pela tela antiga;
- código de animação/estado específico do DOM substituído pelos widgets Flutter;
- helpers e stubs exclusivos da implementação HTML/JS;
- ramo de “reativar tarefa permanente” do cliente antigo que não era alcançável pelo próprio `GET /tasks`, que lista somente templates ativos.

Esses itens não representam perda de capacidade disponível ao usuário.

## CI/CD após encerramento

- `.github/workflows/ci.yml`: backend + `flutter analyze` + `flutter test`;
- `.github/workflows/flutter-web.yml`: análise e build web release em mudanças Flutter;
- `.github/workflows/pages.yml`: build e deploy de `flutter_app/build/web` em `main`;
- não existe mais validação de JavaScript legado.

## Critério de encerramento

O diretório `frontend/` só foi removido depois de:
1. mapear os fluxos e endpoints usados pelo cliente antigo;
2. comparar com as telas Flutter;
3. portar as lacunas de comportamento encontradas;
4. adicionar teste Flutter e validação no CI;
5. manter as páginas legais no bundle Flutter.
