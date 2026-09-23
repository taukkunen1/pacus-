# Auditoria final da migração Flutter

Data: 2026-09-23

## Objetivo

Comparar o frontend HTML/CSS/JavaScript legado com o Flutter Web, portar as funcionalidades úteis que ainda não tinham paridade e remover a base antiga para que exista um único frontend oficial.

## Matriz de paridade

| Área | Legado | Flutter após auditoria | Resultado |
|---|---|---|---|
| Login adulto | Sim | Sim | Paridade |
| Login membro por código/PIN | Sim | Sim | Paridade |
| Criar família | Sim | Sim | Paridade |
| Recuperar senha | Sim | Sim | Paridade |
| Política/Termos | Sim | Sim em `flutter_app/web` | Paridade |
| Rotina Hoje | Sim | Sim | Paridade |
| Criar/editar/remover tarefa diária | Sim | Sim | Paridade |
| Ordenar tarefas | Sim | Sim | Paridade |
| Opções de tarefa | Sim | Sim | Paridade |
| Motivo/razão da tarefa | Sim | Sim | Paridade |
| Registro de iniciativa | Sim | Sim | Paridade |
| Motivo de não realização | Sim | Sim | Paridade |
| Planejamento da noite | Sim | Sim | Paridade |
| Reação do adulto | Sim | Sim | Paridade |
| Histórico | Sim | Sim | Paridade |
| Pontos e extrato | Sim | Sim | Paridade |
| Ajuste de saldo | Sim | Sim | Paridade |
| Loja/resgates/aprovação | Sim | Sim | Paridade |
| Configuração familiar | Sim | Sim | Paridade |
| Growth stages | Sim | Sim | Paridade |
| Timer de jogo | Sim | Sim, com pausa/retomada local persistida | Paridade funcional |
| PACUS animado | Sim | Sim | Paridade |
| Atualização na virada do dia | Sim | Portado nesta auditoria | Fechado |
| Feedback de carregamento lento | Sim | Portado nesta auditoria | Fechado |
| Mensagens de reconhecimento de esforço | Sim | Portado nesta auditoria | Fechado |
| Badges de pendências na navegação | Sim | Portado nesta auditoria | Fechado |
| Amanhã/autonomia antecipada | Não existia na navegação antiga | Sim | Flutter superior |
| Chat familiar | Não | Sim | Flutter superior |
| Valor de 1 PP configurável pelo adulto | Não tinha UI | Sim | Flutter superior |
| Ajuste administrativo do estado do PACUS | Não tinha UI equivalente | Sim | Flutter superior |

## Código legado deliberadamente não portado

### Wrappers `pauseGameTimer` e `resumeGameTimer`

Existiam em `frontend/js/api/pacus-api.js`, mas não eram chamados pelas telas do frontend legado. O Flutter possui pausa/retomada da sessão com persistência local e consumo efetivo no backend ao terminar. Portanto, copiar wrappers mortos não adicionaria funcionalidade.

### Fluxo de reativação de tarefa permanente

O legado possuía chamada para `PUT /tasks/{id}/activate`, mas o endpoint usado para listar tarefas permanentes devolve apenas templates ativos. Assim, o botão de reativação não era alcançável de forma consistente pela própria UI antiga. Não foi considerado uma perda funcional da migração.

### Stubs e componentes sem uso

Arquivos antigos de componentes/estado e utilitários exclusivos da implementação vanilla não foram migrados quando não correspondiam a uma funcionalidade de produto.

## Melhorias portadas nesta auditoria

1. **Virada automática do dia**: a tela Hoje observa mudança de data e retorno do app ao primeiro plano, recarregando a rotina quando necessário.
2. **Carregamento lento**: login e carregamento inicial da rotina passam a explicar uma espera prolongada depois de 4 segundos.
3. **Reforço de esforço**: ao concluir uma tarefa, o membro recebe mensagem contextual baseada no período, tipo, pontos e conclusão do dia.
4. **Badges de navegação**: tarefas pendentes aparecem para o membro e resgates pendentes aparecem para o adulto.
5. **Testes Flutter**: a pasta `flutter_app/test` passa a existir e entra no CI.

## Aposentadoria do frontend antigo

Após esta auditoria:
- `frontend/` é removido;
- nenhuma pipeline valida JavaScript legado;
- `flutter_app/` é a única implementação web;
- GitHub Pages continua publicando `flutter_app/build/web`;
- documentação aponta exclusivamente para Flutter.

## Critério de conclusão

A migração é considerada concluída quando:
- o PR passa em `dotnet build/test`;
- `flutter analyze` passa;
- `flutter test` passa;
- `flutter build web` passa;
- o deploy do GitHub Pages conclui após o merge.
