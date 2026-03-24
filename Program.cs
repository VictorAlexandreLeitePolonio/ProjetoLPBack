using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Services;

// Cria o construtor da aplicação — todos os serviços são registrados aqui antes do Build().
var builder = WebApplication.CreateBuilder(args);

// Registra o AppDbContext com SQLite via string de conexão do appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Habilita o uso de Controllers com [ApiController] e roteamento automático.
// JsonStringEnumConverter garante que enums aparecem como "Scheduled" e não como 0.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Configura o Swagger para documentação da API.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serviço em segundo plano — cancela consultas Scheduled com data no passado.
builder.Services.AddHostedService<AppointmentStatusUpdater>();

// Lê as configurações JWT do appsettings.json.
// "!" suprime o aviso de nullable — garantimos que os valores existem no appsettings.
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Configura o esquema de autenticação JWT Bearer.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
         options.Events = new JwtBearerEvents
  {
      OnMessageReceived = context =>
      {
          context.Token = context.Request.Cookies["auth_token"];
          return Task.CompletedTask;
      }
  };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,           // Verifica se o token foi gerado por este servidor.
            ValidateAudience = true,         // Verifica se o token é destinado a este cliente.
            ValidateLifetime = true,         // Rejeita tokens expirados.
            ValidateIssuerSigningKey = true, // Verifica a assinatura do token com a chave secreta.
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

     builder.Services.AddCors(options =>
  {
      options.AddPolicy("Frontend", policy =>
      {
          policy.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // necessário para enviar/receber cookies
      });
  });


// Constrói a aplicação — após essa linha, não é possível registrar novos serviços.
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Seed — popula o banco com dados iniciais para desenvolvimento.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Garante que o banco e as tabelas existem (aplica migrations pendentes).
    db.Database.Migrate();

    // ── Usuários ────────────────────────────────────────────────────────────
    if (!db.Users.Any(u => u.Role == UserRole.Admin))
    {
        db.Users.Add(new User
        {
            Name         = "Admin",
            Email        = "admin@clinica.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role         = UserRole.Admin
        });
        db.SaveChanges();
    }

    if (!db.Users.Any(u => u.Role == UserRole.Fisio))
    {
        db.Users.Add(new User
        {
            Name         = "Dra. Ana Paula",
            Email        = "ana@clinica.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("fisio123"),
            Role         = UserRole.Fisio
        });
        db.SaveChanges();
    }

    // ── Planos ──────────────────────────────────────────────────────────────
    if (!db.Plans.Any())
    {
        db.Plans.AddRange(
            new Plans { Name = "Fisioterapia Mensal",  Valor = 350.00m, TipoPlano = TipoPlano.Mensal,  TipoSessao = TipoSessao.Fisioterapia },
            new Plans { Name = "Pilates Mensal",       Valor = 280.00m, TipoPlano = TipoPlano.Mensal,  TipoSessao = TipoSessao.Pilates      },
            new Plans { Name = "Massagem Avulsa",      Valor =  90.00m, TipoPlano = TipoPlano.Avulso,  TipoSessao = TipoSessao.Massagem     },
            new Plans { Name = "Hidrolipo Avulso",     Valor = 120.00m, TipoPlano = TipoPlano.Avulso,  TipoSessao = TipoSessao.Hidrolipo    },
            new Plans { Name = "Linfedema Mensal",     Valor = 400.00m, TipoPlano = TipoPlano.Mensal,  TipoSessao = TipoSessao.Linfedema    }
        );
        db.SaveChanges();
    }

    // ── Pacientes ───────────────────────────────────────────────────────────
    if (!db.Patients.Any())
    {
        db.Patients.AddRange(
            new Patient { Name = "Maria Oliveira",   Email = "maria@email.com",   CPF = "111.222.333-44", Rg = "12.345.678-9", Phone = "(11) 91234-5678", Rua = "Rua das Flores",   Numero = "10",  Bairro = "Centro",      Cidade = "São Paulo",    Estado = "SP", Cep = "01001-000" },
            new Patient { Name = "João Pereira",     Email = "joao@email.com",    CPF = "222.333.444-55", Rg = "23.456.789-0", Phone = "(11) 92345-6789", Rua = "Av. Brasil",       Numero = "200", Bairro = "Jardim",      Cidade = "São Paulo",    Estado = "SP", Cep = "02002-000" },
            new Patient { Name = "Carla Souza",      Email = "carla@email.com",   CPF = "333.444.555-66", Rg = "34.567.890-1", Phone = "(11) 93456-7890", Rua = "Rua do Comércio",  Numero = "55",  Bairro = "Vila Nova",   Cidade = "Campinas",     Estado = "SP", Cep = "03003-000" },
            new Patient { Name = "Roberto Lima",     Email = "roberto@email.com", CPF = "444.555.666-77", Rg = "45.678.901-2", Phone = "(11) 94567-8901", Rua = "Rua das Palmeiras",Numero = "88",  Bairro = "Boa Vista",   Cidade = "Santo André",  Estado = "SP", Cep = "04004-000" },
            new Patient { Name = "Fernanda Costa",   Email = "fer@email.com",     CPF = "555.666.777-88", Rg = "56.789.012-3", Phone = "(11) 95678-9012", Rua = "Av. Paulista",     Numero = "1000",Bairro = "Bela Vista",  Cidade = "São Paulo",    Estado = "SP", Cep = "05005-000" }
        );
        db.SaveChanges();
    }

    // ── Agendamentos (hoje) ─────────────────────────────────────────────────
    if (!db.Appointments.Any())
    {
        var adminUser = db.Users.First(u => u.Role == UserRole.Admin);
        var patients  = db.Patients.ToList();
        var today     = DateTime.UtcNow.Date;

        db.Appointments.AddRange(
            new Appointment { UserId = adminUser.Id, PatientId = patients[0].Id, AppointmentDate = today.AddHours(8).AddMinutes(0),  Status = AppointmentStatus.Completed  },
            new Appointment { UserId = adminUser.Id, PatientId = patients[1].Id, AppointmentDate = today.AddHours(9).AddMinutes(30), Status = AppointmentStatus.Completed  },
            new Appointment { UserId = adminUser.Id, PatientId = patients[2].Id, AppointmentDate = today.AddHours(11).AddMinutes(0), Status = AppointmentStatus.Scheduled  },
            new Appointment { UserId = adminUser.Id, PatientId = patients[3].Id, AppointmentDate = today.AddHours(14).AddMinutes(0), Status = AppointmentStatus.Scheduled  },
            new Appointment { UserId = adminUser.Id, PatientId = patients[4].Id, AppointmentDate = today.AddHours(15).AddMinutes(30),Status = AppointmentStatus.Cancelled  }
        );
        db.SaveChanges();
    }

    // ── Prontuários ─────────────────────────────────────────────────────────
    if (!db.MedicalRecords.Any())
    {
        var fisio    = db.Users.First(u => u.Role == UserRole.Fisio);
        var patients = db.Patients.ToList();

        db.MedicalRecords.AddRange(
            // Maria Oliveira — 3 prontuários
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[0].Id,
                Titulo = "Avaliação Inicial", Sessao = "Fisioterapia",
                Patologia = "Lombalgia crônica", QueixaPrincipal = "Dor lombar há 6 meses",
                DoencaAntiga = "Hipertensão", DoencaAtual = "Lombalgia",
                Habitos = "Sedentária, trabalha sentada 8h/dia",
                ExamesFisicos = "Flexão lombar limitada a 60°, dor à palpação L4-L5",
                SinaisVitais = "PA: 130/85, FC: 78, SpO2: 98%",
                Medicamentos = "Losartana 50mg", Cirurgias = "Nenhuma",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Alongamentos diários, evitar sobrecarga",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[0].Id,
                Titulo = "Sessão 5 — Evolução", Sessao = "Fisioterapia",
                Patologia = "Lombalgia crônica", QueixaPrincipal = "Melhora parcial da dor",
                DoencaAntiga = "Hipertensão", DoencaAtual = "Lombalgia em remissão",
                Habitos = "Iniciou caminhadas 3x/semana",
                ExamesFisicos = "Flexão lombar 75°, dor reduzida",
                SinaisVitais = "PA: 125/80, FC: 72, SpO2: 99%",
                Medicamentos = "Losartana 50mg", Cirurgias = "Nenhuma",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Manter caminhadas, fortalecer core",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[0].Id,
                Titulo = "Alta Fisioterapêutica", Sessao = "Fisioterapia",
                Patologia = "Lombalgia crônica", QueixaPrincipal = "Sem dor",
                DoencaAntiga = "Hipertensão", DoencaAtual = "Assintomática",
                Habitos = "Caminhadas regulares e alongamentos",
                ExamesFisicos = "Flexão lombar 90°, sem dor à palpação",
                SinaisVitais = "PA: 120/78, FC: 70, SpO2: 99%",
                Medicamentos = "Losartana 50mg", Cirurgias = "Nenhuma",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Manter atividades físicas regulares",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },

            // João Pereira — 2 prontuários
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[1].Id,
                Titulo = "Avaliação Pós-Cirúrgica", Sessao = "Pilates",
                Patologia = "Pós-operatório de meniscectomia", QueixaPrincipal = "Dor e rigidez no joelho direito",
                DoencaAntiga = "Nenhuma", DoencaAtual = "Pós-op joelho direito",
                Habitos = "Praticava futebol, afastado há 3 meses",
                ExamesFisicos = "Edema leve, ADM 0-90°, força muscular grau 3",
                SinaisVitais = "PA: 118/76, FC: 68, SpO2: 99%",
                Medicamentos = "Ibuprofeno 600mg s/n", Cirurgias = "Meniscectomia parcial joelho D (2 meses atrás)",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Crioterapia 3x/dia, elevação do membro",
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[1].Id,
                Titulo = "Retorno — Semana 4", Sessao = "Pilates",
                Patologia = "Pós-operatório de meniscectomia", QueixaPrincipal = "Dor leve ao subir escadas",
                DoencaAntiga = "Nenhuma", DoencaAtual = "Recuperação progredindo",
                Habitos = "Caminhadas leves autorizadas",
                ExamesFisicos = "Sem edema, ADM 0-120°, força grau 4",
                SinaisVitais = "PA: 116/74, FC: 65, SpO2: 100%",
                Medicamentos = "Sem medicação", Cirurgias = "Meniscectomia parcial joelho D",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Fortalecer quadríceps, bicicleta ergométrica leve",
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            },

            // Carla Souza — 1 prontuário
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[2].Id,
                Titulo = "Avaliação Postural", Sessao = "Pilates",
                Patologia = "Escoliose leve", QueixaPrincipal = "Dor nas costas ao final do dia",
                DoencaAntiga = "Nenhuma", DoencaAtual = "Escoliose leve (15°)",
                Habitos = "Trabalha em pé, usa calçado inadequado",
                ExamesFisicos = "Desvio lateral coluna torácica, encurtamento cadeia posterior",
                SinaisVitais = "PA: 110/70, FC: 74, SpO2: 99%",
                Medicamentos = "Nenhum", Cirurgias = "Nenhuma",
                OutrasDoencas = "Nenhuma", OrientacaoDomiciliar = "Exercícios posturais, calçado adequado",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },

            // Fernanda Costa — 2 prontuários
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[4].Id,
                Titulo = "Avaliação — Drenagem", Sessao = "Hidrolipo",
                Patologia = "Linfedema MMII", QueixaPrincipal = "Inchaço nas pernas após longa jornada",
                DoencaAntiga = "Varizes", DoencaAtual = "Linfedema grau I",
                Habitos = "Trabalha sentada, pouco líquido",
                ExamesFisicos = "Edema bilateral, cacifo positivo, pele tensa",
                SinaisVitais = "PA: 122/80, FC: 80, SpO2: 98%",
                Medicamentos = "Daflon 500mg", Cirurgias = "Nenhuma",
                OutrasDoencas = "Varizes", OrientacaoDomiciliar = "Elevar MMII, meias compressivas, hidratação",
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },
            new MedicalRecord
            {
                UserId = fisio.Id, PatientId = patients[4].Id,
                Titulo = "Sessão 3 — Drenagem", Sessao = "Hidrolipo",
                Patologia = "Linfedema MMII", QueixaPrincipal = "Melhora do inchaço",
                DoencaAntiga = "Varizes", DoencaAtual = "Linfedema em controle",
                Habitos = "Aumentou ingestão de água",
                ExamesFisicos = "Edema reduzido, cacifo negativo",
                SinaisVitais = "PA: 120/78, FC: 76, SpO2: 99%",
                Medicamentos = "Daflon 500mg", Cirurgias = "Nenhuma",
                OutrasDoencas = "Varizes", OrientacaoDomiciliar = "Manter meias compressivas e hidratação",
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            }
        );
        db.SaveChanges();
    }

    // ── Pagamentos ──────────────────────────────────────────────────────────
    if (!db.Payments.Any())
    {
        var adminUser = db.Users.First(u => u.Role == UserRole.Admin);
        var patients  = db.Patients.ToList();
        var plans     = db.Plans.ToList();
        var mes       = DateTime.UtcNow.ToString("yyyy-MM");

        var planFisio   = plans.First(p => p.TipoSessao == TipoSessao.Fisioterapia);
        var planPilates = plans.First(p => p.TipoSessao == TipoSessao.Pilates);
        var planMassagem = plans.First(p => p.TipoSessao == TipoSessao.Massagem);

        db.Payments.AddRange(
            new Payment { UserId = adminUser.Id, PatientId = patients[0].Id, PlanId = planFisio.Id,    Amount = planFisio.Valor,    ReferenceMonth = mes, PaymentMethod = "Pix",     Status = PaymentStatus.Paid,    PaidAt = DateTime.UtcNow.AddDays(-5) },
            new Payment { UserId = adminUser.Id, PatientId = patients[1].Id, PlanId = planPilates.Id,  Amount = planPilates.Valor,  ReferenceMonth = mes, PaymentMethod = "Cartão",  Status = PaymentStatus.Paid,    PaidAt = DateTime.UtcNow.AddDays(-3) },
            new Payment { UserId = adminUser.Id, PatientId = patients[2].Id, PlanId = planFisio.Id,    Amount = planFisio.Valor,    ReferenceMonth = mes, PaymentMethod = "Dinheiro",Status = PaymentStatus.Pending, PaidAt = null                        },
            new Payment { UserId = adminUser.Id, PatientId = patients[3].Id, PlanId = planMassagem.Id, Amount = planMassagem.Valor, ReferenceMonth = mes, PaymentMethod = "Pix",     Status = PaymentStatus.Pending, PaidAt = null                        }
        );
        db.SaveChanges();
    }
}


// Ordem dos middlewares importa:
// 1. CORS — deve vir antes de Authentication/Authorization para permitir que o frontend envie o token.
// 2. StaticFiles — deve vir antes de Authentication/Authorization para servir arquivos públicos sem exigir autenticação.
// 3. Authentication — identifica quem é o usuário pelo token.
// 4. Authorization — verifica se o usuário tem permissão para o endpoint.
// 5. HttpsRedirection — redireciona HTTP para HTTPS.
app.UseCors("Frontend");
app.UseStaticFiles(); // Habilita servir arquivos estáticos (ex: imagens, CSS, JS) da pasta wwwroot.
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

// Mapeia automaticamente as rotas definidas nos Controllers.
app.MapControllers();

// Sobe o servidor.
app.Run();
