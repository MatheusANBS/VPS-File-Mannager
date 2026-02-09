# Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Versionamento Semântico](https://semver.org/lang/pt-BR/).

## [1.1.0] - 2026-02-09

### Adicionado
- ✨ **Editor aprimorado**
  - Detecção automática de comentários (`//` e `#`) em todos os arquivos
  - Suporte a arquivos especiais (`.env`, `.gitignore`, `dockerfile`, `makefile`)
  - Highlighting genérico para arquivos sem definição específica
  
- 🎨 **VS Code Dark+ Theme**
  - Cores oficiais do VS Code aplicadas ao editor
  - Comentários: `#6A9955` (verde)
  - Strings: `#CE9178` (salmão)
  - Keywords: `#569CD6` (azul)
  - Numbers: `#B5CEA8` (verde claro)
  - Functions: `#DCDCAA` (amarelo)
  - Types: `#4EC9B0` (ciano)
  
- 📦 **Sistema de ícones profissional**
  - 10 ícones do [Feather Icons](https://feathericons.com)
  - 11 ícones do [Simple Icons](https://simpleicons.org)
  - Cor padronizada: `#58A6FF` (GitHub blue)
  - Assets organizados em `assets/icons/`
  
- 🛠️ **Build simplificado**
  - Arquivo `build.bat` para compilação com duplo clique
  - Documentação atualizada (sem releases, apenas build local)

### Modificado
- 📚 **Documentação profissionalizada**
  - Emojis removidos do README
  - Screenshots organizados em `assets/`
  - Ícones SVG locais (sem dependências de CDN)
  - Instruções de instalação simplificadas

### Corrigido
- 🐛 Editor agora reconhece comentários em linguagens sem highlighting nativo
- 🔧 Detecção de tipo de arquivo por nome completo (não apenas extensão)

---

## [1.0.0] - 2026-02-06

### Adicionado
- 📁 Gerenciador de arquivos SFTP completo
  - Upload/Download com barra de progresso
  - Drag & Drop do Windows
  - Multi-seleção de arquivos
  - Navegação com histórico (voltar/avançar)
  - Favoritos por conexão
  - Busca recursiva com glob patterns
  
- 💻 Terminal SSH embutido
  - Emulador VT100/xterm-256color completo
  - Suporte a 256 cores e True Color (RGB)
  - Buffer alternativo (vim, htop, nano)
  - Scrollback de 10.000 linhas
  - Redimensionamento dinâmico
  - Copiar/colar integrado
  
- ✏️ Editor de arquivos integrado
  - Syntax highlighting (40+ linguagens)
  - Salvar direto no servidor
  - Detecção de alterações não salvas
  
- ⚡ Sistema de Tasks
  - Comandos pré-configurados
  - Integração com PM2
  - Suporte a sudo com prompt de senha
  
- 🔐 Gerenciamento de conexões
  - Salvar conexões com nome
  - Auto-connect na inicialização
  - Credenciais criptografadas (DPAPI)
  - Autenticação por chave privada

- 🎨 Interface moderna
  - Design Fluent (WPF-UI 3.0)
  - Ícones coloridos por tipo de arquivo
  - Tema escuro

### Segurança
- Senhas criptografadas com DPAPI
- Sem telemetria ou coleta de dados

---

## Tipos de Mudanças

- `Adicionado` para novas funcionalidades
- `Modificado` para mudanças em funcionalidades existentes
- `Descontinuado` para funcionalidades que serão removidas
- `Removido` para funcionalidades removidas
- `Corrigido` para correções de bugs
- `Segurança` para vulnerabilidades
