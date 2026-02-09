<p align="center">
  <img src="assets/icons/linux.svg" alt="Linux" width="80" height="80" />
</p>

<h1 align="center">VPS File Manager</h1>

<p align="center">
  <strong>Gerenciador de arquivos para VPS via SSH/SFTP com terminal embutido</strong>
</p>

<p align="center">
  <em>Integração completa: gerenciamento de arquivos, terminal e editor em um único aplicativo</em>
</p>

<p align="center">
  <!-- Tech Stack Badges -->
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8.0" />
  <img src="https://img.shields.io/badge/WPF-UI%203.0-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="WPF-UI" />
  <img src="https://img.shields.io/badge/Monaco-Editor-007ACC?style=for-the-badge&logo=visualstudiocode&logoColor=white" alt="Monaco Editor" />
  <img src="https://img.shields.io/badge/SSH.NET-2023.0.1-green?style=for-the-badge&logo=gnubash&logoColor=white" alt="SSH.NET" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="License" />
</p>

<p align="center">
  <!-- Stats & Status Badges (com cache para evitar rate limiting) -->
  <img src="https://img.shields.io/github/v/release/MatheusANBS/VPS-File-Mannager?style=flat-square&cacheSeconds=3600" alt="Latest Release" />
  <img src="https://img.shields.io/badge/Status-Active-brightgreen?style=flat-square" alt="Status" />
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Windows" />
</p>

---

<p align="center">
  <img src="assets/main_full.png" alt="VPS File Manager — Sidebar Expanded" width="49%" />
  <img src="assets/main_colapsed.png" alt="VPS File Manager — Sidebar Collapsed" width="49%" />
</p>

<p align="center">
  <em>Sidebar expandida com seções organizadas &bull; Sidebar colapsada em icon strip</em>
</p>

---

## <img src="assets/icons/rocket.svg" width="24" height="24" /> Início Rápido

```bash
# 1. Clone o repositório
git clone https://github.com/MatheusANBS/VPS-File-Mannager.git
cd VPS-File-Mannager

# 2. Execute o instalador
.\build.bat

# 3. Pronto! O app será aberto automaticamente
```

**Primeira conexão:**
1. Clique em "Nova Conexão"
2. Preencha host, usuário e senha
3. Conectar e começar a usar!

---

## <img src="assets/icons/lightbulb.svg" width="24" height="24" /> Por que VPS File Manager?

| <img src="assets/icons/cross.svg" width="16" height="16" /> **Antes** | <img src="assets/icons/check.svg" width="16" height="16" /> **Agora** |
|---|---|
| <img src="assets/icons/folder.svg" width="16" height="16" /> **WinSCP** → Upload/Download | <img src="assets/icons/lightning.svg" width="16" height="16" /> **1 aplicativo integrado** |
| <img src="assets/icons/monitor.svg" width="16" height="16" /> **PuTTY** → Terminal | <img src="assets/icons/target.svg" width="16" height="16" /> **Workflow contínuo** |
| <img src="assets/icons/note.svg" width="16" height="16" /> **Notepad++** → Editar configs | <img src="assets/icons/wind.svg" width="16" height="16" /> **3x mais produtivo** |
| <img src="assets/icons/refresh.svg" width="16" height="16" /> **Alt+Tab** → Trocar entre 3 apps | <img src="assets/icons/palette.svg" width="16" height="16" /> **Interface moderna** |
| <img src="assets/icons/clock.svg" width="16" height="16" /> Tempo perdido trocando contexto | <img src="assets/icons/lock.svg" width="16" height="16" /> **Mais seguro (DPAPI)** |

### O Problema que Resolvemos

Gerenciar servidores VPS exige múltiplas ferramentas desconectadas. Você perde tempo trocando entre apps (WinSCP, PuTTY, editor), re-autenticando em cada ferramenta e perdendo o contexto de onde estava.

**VPS File Manager unifica tudo**, mantendo você no fluxo.

---

## <img src="assets/icons/chart-bar.svg" width="24" height="24" /> Comparação com Alternativas

| Feature | WinSCP | FileZilla | PuTTY + WinSCP | **VPS File Manager** |
|---------|--------|-----------|----------------|---------------------|
| <img src="assets/icons/folder.svg" width="16" height="16" /> SFTP Browser | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> |
| <img src="assets/icons/upload.svg" width="16" height="16" /> Upload/Download | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> |
| <img src="assets/icons/monitor.svg" width="16" height="16" /> Terminal SSH | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/warning.svg" width="16" height="16" /> (separado) | <img src="assets/icons/check.svg" width="16" height="16" /> **integrado** |
| <img src="assets/icons/note.svg" width="16" height="16" /> Editor de Código | <img src="assets/icons/warning.svg" width="16" height="16" /> básico | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> **Monaco (VS Code)** |
| <img src="assets/icons/palette.svg" width="16" height="16" /> UI Moderna (Fluent) | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> |
| <img src="assets/icons/task.svg" width="16" height="16" /> Tasks/Comandos | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> |
| <img src="assets/icons/refresh.svg" width="16" height="16" /> PM2 Integration | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> |
| <img src="assets/icons/chart-bar.svg" width="16" height="16" /> Dashboard (CPU/RAM/Disk) | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> **real-time** |
| <img src="assets/icons/save.svg" width="16" height="16" /> Favoritos | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> | <img src="assets/icons/cross.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> **por conexão** |
| <img src="assets/icons/target.svg" width="16" height="16" /> Context Switching | Alto | Alto | Muito Alto | **Zero** |
| <img src="assets/icons/key.svg" width="16" height="16" /> Segurança de Senhas | <img src="assets/icons/warning.svg" width="16" height="16" /> | <img src="assets/icons/warning.svg" width="16" height="16" /> | <img src="assets/icons/warning.svg" width="16" height="16" /> | <img src="assets/icons/check.svg" width="16" height="16" /> **DPAPI** |

---

## <img src="assets/icons/target.svg" width="24" height="24" /> Para Quem é Este Projeto?

<table>
<tr>
<td width="50%">

### <img src="assets/icons/laptop.svg" width="16" height="16" /> Desenvolvedores Web
- Deploy rápido de aplicações Node/PHP
- Debug direto no servidor
- Editar configs sem baixar/subir
- Restart de serviços (nginx, pm2)

</td>
<td width="50%">

### <img src="assets/icons/wrench.svg" width="16" height="16" /> DevOps / SysAdmins
- Gerenciar múltiplos servidores
- Executar tasks repetitivas
- **Dashboard integrado** (CPU, RAM, Disk, Network)
- Scripts de manutenção

</td>
</tr>
<tr>
<td>

### <img src="assets/icons/books.svg" width="16" height="16" /> Estudantes
- Aprender Linux sem VM local
- Praticar comandos SSH
- Ambiente real de desenvolvimento
- Interface amigável para iniciantes

</td>
<td>

### <img src="assets/icons/building.svg" width="16" height="16" /> Equipes
- Acesso padronizado a servidores
- Compartilhar configs/tasks
- Sem necessidade de admin
- Onboarding mais rápido

</td>
</tr>
</table>

---

## <img src="assets/icons/star.svg" width="20" height="20" /> Funcionalidades em Destaque

### <img src="assets/icons/files.svg" width="20" height="20" /> Gerenciador de Arquivos
- Upload/Download com progresso
- Drag & Drop do Windows
- Navegação com histórico (voltar/avançar)
- Favoritos por conexão
- Busca recursiva com glob patterns
- Multi-seleção e operações em lote
- **Sidebar colapsável** com seções organizadas (File Actions, Favorites, Tools)
- Detecção de **alterações não salvas** com diálogo de confirmação

### <img src="assets/icons/gnometerminal.svg" width="20" height="20" /> Terminal Embutido (TTY Real)
<p align="center">
  <img src="assets/htop.png" alt="Terminal rodando htop" width="100%" />
</p>

- Emulador VT100/xterm-256color completo
- Roda `htop`, `vim`, `nano`, `mc` perfeitamente
- 256 cores + True Color (RGB)
- Scrollback de 10.000 linhas
- Copiar/colar funciona de verdade

### <img src="assets/icons/editor.svg" width="20" height="20" /> Editor de Código (Monaco — Engine do VS Code)
<p align="center">
  <img src="assets/editor.png" alt="Editor com Monaco Editor" width="100%" />
</p>

- **Monaco Editor** embarcado (mesmo engine do VS Code)
- Syntax highlighting (60+ linguagens)
- IntelliSense, autocomplete e code folding
- Minimap, word wrap e tamanho de fonte ajustável
- Toolbar visual com todos os comandos acessíveis
- Command Palette (F1) e Go to Line (Ctrl+G)
- Find & Replace com regex
- Barra de status com posição do cursor, seleção, linguagem e encoding
- Salva direto no servidor (Ctrl+S)
- Detecta alterações não salvas
- Suporte a `.env`, `.json`, `.yaml`, `.py`, `.js`, `.ts`, `.rs`, `.go`...

### <img src="assets/icons/chart-bar.svg" width="20" height="20" /> Dashboard de Monitoramento

<p align="center">
  <img src="assets/dashboard.png" alt="Dashboard — CPU, RAM, Disk, Network, Processes" width="100%" />
</p>

- **Métricas em tempo real** do servidor via SSH (leitura de /proc)
- **CPU Usage** com sparkline de histórico e percentual em tempo real
- **Memory Usage** com sparkline de histórico (usado/total)
- **Disk Usage** com gráfico donut visual (espaço usado/livre)
- **Network Interfaces** com IP, tráfego TX/RX e velocidade
- **Uptime** do servidor com ícone de heartbeat
- **System Info** — hostname, OS, kernel, CPUs, load average
- **Top Processes** — tabelas de processos por CPU e por Memória
- Refresh automático configurável (padrão: 3s)
- Color-coding por threshold (verde → amarelo → vermelho)

### <img src="assets/icons/task.svg" width="20" height="20" /> Automação de Tasks
<p align="center">
  <img src="assets/task.png" alt="Tasks Window" width="100%" />
</p>

- **13 comandos pré-configurados** prontos para uso
- **Toggle sudo** — ative/desative sudo para todos os comandos (ideal para servidores root)
- Suporte a `sudo` com prompt de senha seguro
- Seleção dinâmica de processos (carrega lista via SSH)

**PM2:**
| Comando | Descrição |
|---------|----------|
| PM2 Restart All | Reinicia todos os processos |
| PM2 Restart Specific | Reinicia um processo específico |
| PM2 Stop All | Para todos os processos |
| PM2 Stop Specific | Para um processo específico |
| PM2 Delete Specific | Remove um processo do PM2 |
| PM2 Status | Exibe status de todos os processos |
| PM2 Logs | Últimas 50 linhas de log (todos) |
| PM2 Logs Specific | Logs de um processo específico |
| PM2 Save | Salva lista para auto-restart |

**Nginx:**
| Comando | Descrição |
|---------|----------|
| Nginx Restart | Reinicia o serviço |
| Nginx Stop | Para o serviço |
| Nginx Status | Exibe status do serviço |
| Nginx Test Config | Testa configuração por erros |

---

## <img src="assets/icons/keyboard.svg" width="20" height="20" /> Atalhos de Teclado

<details>
<summary><strong>Clique para ver todos os atalhos</strong></summary>

| Atalho | Ação | Contexto |
|--------|------|----------|
| `Ctrl+R` | Atualizar | Qualquer lugar |
| `Ctrl+U` | Upload | File Manager |
| `Ctrl+D` | Download | File Manager |
| `Ctrl+N` | Nova pasta | File Manager |
| `Delete` | Deletar | File Manager |
| `F2` | Renomear | File Manager |
| `Ctrl+H` | Ir para root (`/`) | File Manager |
| `Backspace` | Subir diretório | File Manager |
| `Alt+←` | Voltar | Navegação |
| `Alt+→` | Avançar | Navegação |
| `Ctrl+F` | Busca avançada | File Manager |
| `Ctrl+A` | Selecionar tudo | File Manager |
| `Ctrl+L` | Editar path | File Manager |
| `Ctrl+S` | Salvar | Editor |
| `Ctrl+F` | Buscar | Editor |
| `Ctrl+H` | Buscar & Substituir | Editor |
| `Ctrl+G` | Ir para Linha | Editor |
| `Ctrl+Z` | Desfazer | Editor |
| `Ctrl+Y` | Refazer | Editor |
| `Ctrl+W` | Fechar editor | Editor |
| `Shift+Alt+F` | Formatar documento | Editor |
| `Alt+Z` | Toggle Word Wrap | Editor |
| `F1` | Command Palette | Editor |
| `Ctrl+C` | Interromper (SIGINT) | Terminal |
| `Ctrl+D` | EOF/Logout | Terminal |

</details>

---

## <img src="assets/icons/dotnet.svg" width="20" height="20" /> Stack Tecnológica

```
┌─────────────────────────────────────────────────────────────┐
│                      VPS File Manager                        │
├─────────────────────────────────────────────────────────────┤
│  UI Layer                                                    │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  WPF-UI 3.0 │  │Monaco Editor│  │ Custom Terminal     │  │
│  │  (Fluent)   │  │ (WebView2)  │  │ Control (xterm-256) │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  Dashboard  │  │  Sparkline  │  │ Donut Chart         │  │
│  │  (Metrics)  │  │  Control    │  │ (Disk Usage)        │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  Logic Layer (MVVM)                                          │
│  ┌─────────────────────────────────────────────────────────┐│
│  │          CommunityToolkit.Mvvm 8.2.2                    ││
│  │     (ObservableObject, RelayCommand, etc.)              ││
│  └─────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────┤
│  Services Layer                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  SSH.NET    │  │ DPAPI       │  │ VirtualTerminal     │  │
│  │  (SFTP/SSH) │  │ (Crypto)    │  │ (xterm emulator)    │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  .NET 8.0                                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## <img src="assets/icons/lock.svg" width="24" height="24" /> Segurança

<table>
<tr>
<td width="33%">

### Criptografia de Credenciais
- <img src="assets/icons/check.svg" width="16" height="16" /> Usa **Windows DPAPI**
- <img src="assets/icons/check.svg" width="16" height="16" /> Senhas nunca em texto plano
- <img src="assets/icons/check.svg" width="16" height="16" /> Proteção por usuário Windows
- <img src="assets/icons/check.svg" width="16" height="16" /> Limpeza automática de memória

</td>
<td width="33%">

### Autenticação
- <img src="assets/icons/check.svg" width="16" height="16" /> Senha tradicional
- <img src="assets/icons/check.svg" width="16" height="16" /> Chave privada (PEM/PPK)
- <img src="assets/icons/check.svg" width="16" height="16" /> Suporte a passphrase
- <img src="assets/icons/check.svg" width="16" height="16" /> Múltiplos métodos por conexão

</td>
<td width="34%">

### Privacidade
- <img src="assets/icons/check.svg" width="16" height="16" /> Zero telemetria
- <img src="assets/icons/check.svg" width="16" height="16" /> Zero tracking
- <img src="assets/icons/check.svg" width="16" height="16" /> Dados locais apenas
- <img src="assets/icons/check.svg" width="16" height="16" /> Open source auditável

</td>
</tr>
</table>

**Política de Segurança:** Encontrou uma vulnerabilidade? Por favor, veja [SECURITY.md](SECURITY.md).

---

## <img src="assets/icons/globe.svg" width="24" height="24" /> Comunidade & Contribuição

<p align="center">
  <a href="https://github.com/MatheusANBS/VPS-File-Mannager/discussions">
    <img src="https://img.shields.io/badge/Discussões-GitHub-purple?style=for-the-badge&logo=github" alt="Discussions">
  </a>
  <a href="https://github.com/MatheusANBS/VPS-File-Mannager/graphs/contributors">
    <img src="https://img.shields.io/github/contributors/MatheusANBS/VPS-File-Mannager?style=for-the-badge" alt="Contributors">
  </a>
</p>

Contribuições são bem-vindas!
1. Fork o projeto
2. Crie uma branch (`git checkout -b feature/MinhaFeature`)
3. Commit suas mudanças (`git commit -m 'Add: Minha nova feature'`)
4. Push para a branch (`git push origin feature/MinhaFeature`)
5. Abra um Pull Request

> Leia o **[CONTRIBUTING.md](CONTRIBUTING.md)** para guias detalhados.

---

## <img src="assets/icons/rocket.svg" width="24" height="24" /> Pronto para Começar?

<p align="center">
  <a href="https://github.com/MatheusANBS/VPS-File-Mannager/releases/latest">
    <img src="https://img.shields.io/badge/BAIXAR-Última_Versão-blue?style=for-the-badge&logo=github" alt="Download">
  </a>
  <a href="https://github.com/MatheusANBS/VPS-File-Mannager/wiki">
    <img src="https://img.shields.io/badge/LER-Documentação-green?style=for-the-badge&logo=readthedocs" alt="Docs">
  </a>
  <a href="https://github.com/MatheusANBS/VPS-File-Mannager/issues">
    <img src="https://img.shields.io/badge/REPORTAR-Bugs-red?style=for-the-badge&logo=github" alt="Issues">
  </a>
</p>

<p align="center">
  <strong>Se este projeto economizou seu tempo, considere deixar uma <img src="assets/icons/star.svg" width="16" height="16" />!</strong>
</p>

---

<p align="center">
  <sub>VPS File Manager © 2024-2026 - Licença MIT</sub>
</p>
