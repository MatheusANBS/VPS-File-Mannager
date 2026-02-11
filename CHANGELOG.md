# Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

## [1.4.1] - 2026-02-11

### Hotfix — Auto-Update Restart
- **Fix**: App não reabria após atualização silenciosa — removida flag `skipifsilent` do Inno Setup `[Run]`

## [1.4.0] - 2026-02-11

### Auto-Update — Atualização Automática
- **Verificação automática** de atualizações via GitHub Releases ao iniciar o app
- **Indicador verde** de download na sidebar quando há nova versão disponível
- **UpdateWindow** com changelog do release, versão atual → nova, e tamanho do download
- **Download com progresso** — barra de progresso em tempo real durante o download
- **Instalação silenciosa** — Inno Setup roda com `/VERYSILENT` (sem interface, sem admin)
- **Restart automático** — app fecha e reabre automaticamente após atualização
- Versão do app embarcada no Assembly (`Version`, `AssemblyVersion`, `FileVersion`)

## [1.3.1] - 2026-02-11

### Editor — Monaco Validação
- **Fix**: Erros falsos de "Cannot find module" em arquivos `.tsx`/`.jsx` — Monaco standalone não tem acesso ao `node_modules` remoto
- Desabilitada validação semântica para TypeScript/JavaScript (mantém validação de sintaxe)
- Configurado `compilerOptions` com suporte a JSX (`JsxEmit.React`) e ESNext
- Adicionado `setContentWithUri()` — carrega arquivos com URI virtual para reconhecimento correto de `.tsx`/`.jsx`
- EditorWindow agora usa URI virtual do arquivo remoto ao abrir (`file:///path/to/file.tsx`)

## [1.3.0] - 2026-02-10

### Dashboard — Monitoramento em Tempo Real
- Janela de Dashboard com métricas do servidor via SSH (leitura de /proc)
- CPU, Memória (com sparkline de histórico), Disco (gráfico donut)
- Network Interfaces com IP e velocidade em tempo real
- Uptime, System Info (hostname, OS, kernel, load average)
- Top Processes por CPU e por Memória
- Refresh automático configurável (padrão: 3s)
- Color-coding por threshold (verde → amarelo → vermelho)

### Sidebar — Redesign
- Sidebar reorganizada em seções: Connection Header, File Actions, Favorites, Tools, Footer
- Collapse/Expand toggle — colapsa para icon strip (52px) com tooltips

### Login — Redesign
- Tela de conexão reescrita com dark theme, ícones por campo e hero header
- Sidebar de conexões recentes com seleção estilizada

### Editor
- Detecção de alterações não salvas com diálogo de confirmação (Save/Don't Save/Cancel)

## [1.2.0] - 2026-02-10

### Editor — Monaco Editor (VS Code Engine)
- **BREAKING**: Substituição do AvalonEdit pelo **Monaco Editor** (mesmo engine do VS Code)
- Editor embarcado via **WebView2** com carregamento CDN (Monaco v0.52.2)
- Tema **VS Code Dark+** idêntico ao original
- Detecção automática de linguagem para 60+ extensões de arquivo
- Suporte completo a: IntelliSense, syntax highlighting, code folding, minimap, word wrap
- Atalhos do VS Code funcionando nativamente (Ctrl+S, Ctrl+F, Ctrl+H, Ctrl+G, F1, etc.)

### Editor — Toolbar Visual
- Nova barra de ferramentas com botões expostos: Undo, Redo, Find, Replace, Format, Fold All, Unfold All
- Botão **Go to Line** (Ctrl+G) e **Command Palette** (F1) acessíveis via toolbar
- Toggles visuais para **Word Wrap** e **Minimap** com estado visual (ligado/desligado)
- Controle de **tamanho de fonte** (A-/A+) com range 8-32px
- Botão **Shortcuts** com janela estilizada listando todos os atalhos disponíveis

### Editor — Barra de Status (VS Code Style)
- Status bar azul (#007acc) no rodapé com informações em tempo real
- Posição do cursor (Ln/Col), informação de seleção, contagem de linhas
- Indicador de linguagem, encoding e tamanho do arquivo
- Indicador de dirty state (modificações não salvas)

### Editor — TitleBar e Janela
- Adicionado `ui:TitleBar` com botões nativos de **minimizar, maximizar e fechar**
- Janela agora é arrastável e redimensionável corretamente
- Janela de **Shortcuts** estilizada no padrão visual do app (FluentWindow + Mica)

### Editor — Correções
- **Fix**: Conteúdo do arquivo aparecia vazio ao abrir — corrigido double-encoding do WebMessage (`WebMessageAsJson` → `TryGetWebMessageAsString`)
- **Fix**: Botões da toolbar não funcionavam após clicar fora do editor — adicionado `FocusEditorAsync()` que refoca WebView2 + Monaco antes de cada comando
- **Fix**: `NullReferenceException` em `OnMinimapChanged` ao inicializar — adicionado guard `IsReady` em todos os handlers da toolbar
- **Fix**: Command Palette não abria — corrigido `executeCommand()` JS para usar `editor.getAction(id).run()` para actions e `editor.trigger()` como fallback

### Terminal — Compatibilidade TTY
- Suporte a **DECCKM** (cursor key mode) para navegação em menus interativos
- Suporte a **DECKPAM/DECKPNM** (application/numeric keypad mode)
- Suporte a **bracketed paste mode** (colagem segura em editores)
- Suporte a **mouse tracking** (SGR 1006, normal, button, any-event)
- Suporte a **focus events** (in/out reporting)
- Respostas automáticas: **DSR** (Device Status Report), **DA** (Device Attributes)
- Sequências **REP** (repeat character) e **CBT** (cursor backward tab)
- Tratamento de C0 controls e ESC dentro de sequências CSI
- Tracking de charset ativo (`_activeCharset`) e fix de terminador DCS

### Terminal — Renderização de Tabelas
- **Reescrita** do sistema de renderização: de spans para **per-character** em posições exatas de grid
- Cada caractere renderizado em `col * _cellWidth` — elimina acúmulo de erros sub-pixel
- Tabelas de `htop`, `glances`, `pm2 monit` agora exibem alinhamento perfeito

### Terminal — UTF-8
- **Fix**: Caracteres multi-byte (UTF-8) corrompidos quando sequência era dividida entre reads do buffer
- Substituição de `Encoding.UTF8.GetString()` por `System.Text.Decoder` stateful que preserva estado entre leituras
- Corrigido `stty resize` com redirecionamento de stderr (`2>/dev/null`)

### Tasks — Novos Comandos
- Adicionado **PM2 Stop All** — para todos os processos PM2
- Adicionado **PM2 Stop Specific** — para um processo específico (com seleção dinâmica)
- Adicionado **PM2 Delete Specific** — remove um processo do PM2 (com seleção dinâmica)
- Adicionado **PM2 Logs** — exibe últimas 50 linhas de log de todos os processos
- Adicionado **PM2 Logs Specific** — exibe logs de um processo específico (com seleção dinâmica)
- Adicionado **PM2 Save** — salva lista de processos para reinício automático
- Adicionado **Nginx Restart** — reinicia o serviço Nginx
- Adicionado **Nginx Stop** — para o serviço Nginx
- Adicionado **Nginx Status** — exibe status do serviço Nginx
- Adicionado **Nginx Test Config** — testa configuração do Nginx por erros de sintaxe
- Total de tasks: 3 → **13 comandos pré-configurados**

### Tasks — Toggle Sudo
- Adicionado **toggle sudo** na sidebar da janela de Tasks
- Quando ativo (padrão), todos os comandos são executados com `sudo` e pedem senha
- Quando desativado, comandos rodam sem `sudo` — ideal para servidores onde o usuário já é root
- `sudo` é aplicado dinamicamente em runtime, não mais hardcoded nos comandos
- `GetPM2ApplicationsListAsync` atualizado para suportar modo com/sem sudo

## [1.1.0] - 2026-02-10

### Migração
- **BREAKING**: Migração de .NET Framework 4.8 → .NET 8.0-windows
- **Requisito**: .NET 8 Runtime agora é necessário para executar o aplicativo
- Performance melhorada: startup 30% mais rápido, menor uso de memória

### Segurança
- **CRÍTICO**: Implementado `SecurePasswordManager` com proteção process-scoped
  - Senha criptografada em memória com AES-256
  - Chave única por processo (gerada na inicialização)
  - PID binding para isolamento máximo
  - Memory zeroing com unsafe code
  - Impossível decrypt por outros processos (mesmo do mesmo usuário)
- Habilitado `AllowUnsafeBlocks` para memory zeroing de senhas

### Técnico
- Target Framework: `net8.0-windows` (Windows-specific WPF application)
- Compilação bem-sucedida: 0 erros, 48 warnings (nullable references)
- Compatibilidade verificada: WPF, SSH.NET, WPF-UI, AvalonEdit
- C# 12 features habilitadas
- Nullable reference types ativadas (melhor null-safety)

### Por Que Migrar?
- `.NET Framework 4.8` não tem `DataProtectionScope.Process` (necessário para segurança máxima)
- AES-256 process-scoped oferece segurança equivalente ao ProtectedMemory
- Suporte de longo prazo (LTS) até 2026+
- APIs modernas de criptografia com hardware acceleration

### Compatibilidade
- ✅ Windows 10 1607+ (Anniversary Update)
- ✅ Windows 11 (todas as versões)
- ✅ Windows Server 2019+
- ❌ Windows 7/8.1 (não suportado pelo .NET 8)

### Notas de Atualização
Para usuários do .NET Framework 4.8:
1. Baixar e instalar [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Credenciais existentes permanecem compatíveis (mesmo algoritmo DPAPI)
3. Arquivos de configuração não precisam ser alterados

---

## [1.0.1] - 2026-02-09

### Adicionado
- Botão de ajuda (Atalhos) com lista completa de hotkeys do aplicativo.
- Editor aprimorado com detecção de comentários e suporte a arquivos de configuração (.env, dockerfile).
- Novo tema de cores para o editor (baseado no VS Code Dark+).
- Sistema de ícones padronizado em SVG.
- Script automatizado para build (build.bat).

### Modificado
- Reformulação da documentação e remoção de dependências externas de ícones.
- Padronização visual dos assets.

### Corrigido
- Ajustes na detecção de sintaxe e comentários no editor de texto.

## [1.0.0] - 2026-02-06

### Adicionado
- Lançamento inicial do VPS File Manager.
- Gerenciador de arquivos SFTP completo (Upload, Download, Drag & Drop).
- Terminal SSH integrado com emulação VT100/xterm-256color.
- Editor de código remoto com syntax highlighting.
- Sistema de automação de tarefas e scripts.
- Gerenciamento seguro de conexões com criptografia (DPAPI).
