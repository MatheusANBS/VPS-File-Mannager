# Política de Segurança

## Versões Suportadas

| Versão | Suportada          |
| ------ | ------------------ |
| 1.x.x  | :white_check_mark: |

## Reportando uma Vulnerabilidade

Se você descobrir uma vulnerabilidade de segurança, por favor **NÃO** abra uma Issue pública.

### Como Reportar

1. Envie um email para o mantenedor do projeto (via GitHub)
2. Ou use a funcionalidade [Security Advisories](../../security/advisories) do GitHub

### O que incluir no report

- Descrição detalhada da vulnerabilidade
- Passos para reproduzir
- Impacto potencial
- Sugestões de correção (se tiver)

### O que esperar

- Confirmação de recebimento em até 48 horas
- Avaliação inicial em até 7 dias
- Atualizações regulares sobre o progresso
- Crédito na correção (se desejar)

## Práticas de Segurança do Projeto

### Armazenamento de Credenciais

- Senhas são criptografadas usando **DPAPI** (Windows Data Protection API)
- Credenciais nunca são salvas em texto plano
- Chaves privadas SSH são armazenadas apenas como referência ao arquivo

### Conexões SSH

- Usa biblioteca **SSH.NET** com criptografia padrão da indústria
- Suporta autenticação por senha ou chave privada
- Conexões são estabelecidas diretamente, sem proxies intermediários

### Dados do Usuário

- Nenhum dado é enviado para servidores externos
- Nenhuma telemetria ou analytics
- Configurações são armazenadas localmente em `%APPDATA%`

## Boas Práticas para Usuários

1. Mantenha o aplicativo atualizado
2. Use autenticação por chave privada quando possível
3. Não compartilhe suas credenciais
4. Mantenha seu Windows atualizado
