# ProjetoLP — API Back-end

API REST desenvolvida em **.NET 10** para gerenciamento completo de uma clínica de fisioterapia. Este sistema foi construído como projeto pessoal para a clínica da minha mãe, com o objetivo de digitalizar e centralizar toda a operação clínica — de pacientes a controle financeiro.

---

## Para quem é

Sistema interno utilizado pela administração e pelos fisioterapeutas da clínica. Acesso controlado por perfis:

- **Admin** — acesso total: pacientes, agenda, pagamentos, planos, prontuários, usuários e financeiro
- **Fisio** — acesso restrito: pacientes, agenda e prontuários

---

## Tecnologias

| Tecnologia | Uso |
|-----------|-----|
| .NET 10 | Framework principal |
| ASP.NET Core | Web API / Controllers |
| Entity Framework Core 10 | ORM / migrations |
| SQLite | Banco de dados |
| BCrypt.Net | Hash de senhas |
| JWT Bearer | Autenticação via cookies HttpOnly |
| Swagger | Documentação da API |

---

## Módulos

### Autenticação (`/api/auth`)
- Login com JWT armazenado em cookie HttpOnly
- Expiração de 8 horas
- Controle de acesso por roles (`Admin`, `Fisio`)

### Pacientes (`/api/patients`)
- CRUD completo com paginação
- Filtros por nome, status ativo/inativo, status de agendamento e pagamento
- Alternância de status ativo/inativo (PATCH)
- Proteção contra exclusão de pacientes com vínculos

### Agenda (`/api/appointments`)
- Agendamento de consultas com validação de data futura
- Regras de negócio: não reagenda consultas concluídas ou canceladas
- Proteção contra agendamento de pacientes inativos

### Prontuários (`/api/medicalrecords`)
- Registro clínico completo por paciente
- Upload de contrato em PDF (limite 10MB)
- Upload de exames de imagem JPG/PNG (limite 5MB)

### Pagamentos (`/api/payments`)
- Controle de pagamentos mensais por paciente e plano
- Geração automática do valor a partir do plano vinculado
- Gerenciamento automático de `PaidAt` conforme status
- Proteção contra deleção de pagamentos já confirmados

### Planos (`/api/plans`)
- CRUD de planos com valor, tipo (Mensal/Avulso) e tipo de sessão
- Status ativo/inativo para controle de disponibilidade
- Proteção contra exclusão de planos com pagamentos associados

### Usuários (`/api/users`)
- Gerenciamento de usuários do sistema
- Proteção contra exclusão do último administrador

### Financeiro (`/api/financial`)
- Registro de gastos mensais da clínica
- Balanço mensal: soma de gastos vs. soma de pagamentos recebidos
- Histórico de balanço dos últimos N meses (para gráfico de evolução)

---

## Padrões de erro

Todas as respostas de erro seguem o formato padronizado:

```json
{ "message": "Descrição clara do erro." }
```

---

## Como rodar

```bash
# Restaurar dependências
dotnet restore

# Aplicar migrations e subir o banco
dotnet ef database update

# Rodar a API
dotnet run
```

A API sobe em `http://localhost:5062` por padrão. Swagger disponível em `/swagger`.

---

## O que aprendi construindo este projeto

Este foi meu primeiro projeto back-end completo em .NET, saindo do zero até uma API production-ready. As principais habilidades e conceitos que desenvolvi:

- **Arquitetura REST** — organização de rotas, verbos HTTP corretos (GET, POST, PUT, PATCH, DELETE), status codes semânticos (200, 201, 204, 400, 401, 404, 409)
- **Entity Framework Core** — modelagem de entidades, relacionamentos 1:N, migrations, navegação entre tabelas com `Include`, queries com LINQ
- **Autenticação JWT** — geração e validação de tokens, claims, cookies HttpOnly para segurança, controle de acesso por roles com `[Authorize(Roles = "Admin")]`
- **Boas práticas de segurança** — hash de senhas com BCrypt, nunca expor `PasswordHash` na resposta, mensagens de erro que não revelam informações sensíveis
- **DTOs** — separação entre Model (entidade do banco) e DTO (o que o cliente vê), evitando over-posting e vazamento de dados internos
- **Regras de negócio no back-end** — validações que protegem a integridade dos dados independentemente do front-end
- **Tratamento de erros consistente** — respostas padronizadas que o front consegue consumir de forma uniforme
- **Relacionamentos e integridade referencial** — proteções contra exclusão de registros com dependências (ex: não excluir plano com pagamentos)
- **Upload de arquivos** — validação de tipo MIME e tamanho, armazenamento em disco com nomes únicos via GUID
- **Background Services** — serviço automático para cancelar consultas passadas com `IHostedService`
