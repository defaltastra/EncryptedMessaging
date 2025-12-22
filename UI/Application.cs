using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using EncryptedMessaging.Models;
using EncryptedMessaging.Services;

namespace EncryptedMessaging.UI;

public class Application
{
    private readonly AuthService _authService;
    private readonly MessageService _messageService;
    private User? _currentUser;
    private const string ADMIN_USERNAME = "admin";
    private const string ADMIN_PASSWORD = "admin";
    private CancellationTokenSource? _notificationCancellation;

    public Application()
    {
        _authService = new AuthService();
        _messageService = new MessageService();
    }

    // Compte les messages non lus
    private async Task<int> GetUnreadMessageCountAsync()
    {
        if (_currentUser == null) return 0;
        var messages = await _messageService.GetReceivedMessagesAsync(_currentUser.Id);
        return messages.Count(m => !m.IsRead);
    }

    public async Task RunAsync()
    {
        Console.Clear();
        await ShowAnimatedWelcomeAsync();

        while (true)
        {
            if (_currentUser == null)
                await ShowLoginMenuAsync();
            else
                await ShowMainMenuAsync();
        }
    }

    // Écran d'accueil animé
    private async Task ShowAnimatedWelcomeAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("[yellow]Chargement...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("yellow"));
                await Task.Delay(800);
            });

        Console.Clear();
        
        var gradient = new[]
        {
            "[yellow]███████╗███╗   ██╗ ██████╗██████╗ ██╗   ██╗██████╗ ████████╗███████╗██████╗ [/]",
            "[orange3]██╔════╝████╗  ██║██╔════╝██╔══██╗╚██╗ ██╔╝██╔══██╗╚══██╔══╝██╔════╝██╔══██╗[/]",
            "[olive]█████╗  ██╔██╗ ██║██║     ██████╔╝ ╚████╔╝ ██████╔╝   ██║   █████╗  ██║  ██║[/]",
            "[green]██╔══╝  ██║╚██╗██║██║     ██╔══██╗  ╚██╔╝  ██╔═══╝    ██║   ██╔══╝  ██║  ██║[/]",
            "[yellow]███████╗██║ ╚████║╚██████╗██║  ██║   ██║   ██║        ██║   ███████╗██████╔╝[/]",
            "[orange3]╚══════╝╚═╝  ╚═══╝ ╚═════╝╚═╝  ╚═╝   ╚═╝   ╚═╝        ╚═╝   ╚══════╝╚═════╝ [/]"
        };

        foreach (var line in gradient)
        {
            AnsiConsole.MarkupLine(line);
            await Task.Delay(100);
        }

        AnsiConsole.Write(new Rule("[yellow]Système de Messagerie Chiffré[/]") 
            { Style = Style.Parse("olive") });
        
        await Task.Delay(500);
        AnsiConsole.MarkupLine("\n[dim]🔒 Chiffrement AES-256 | 🔑 Hash PBKDF2 | 🛡️ Sécurisé[/]\n");
        await Task.Delay(800);
    }

    private async Task ShowLoginMenuAsync()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]═══[/] [orange3 bold]Bienvenue![/] [yellow]═══[/]\n Que souhaitez-vous faire?")
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices("🔐 Se connecter", "📝 S'inscrire", "❌ Quitter"));

        switch (choice)
        {
            case "🔐 Se connecter":
                await LoginAsync();
                break;
            case "📝 S'inscrire":
                await RegisterAsync();
                break;
            case "❌ Quitter":
                await AnsiConsole.Status().StartAsync("[yellow]Fermeture...[/]", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await Task.Delay(500);
                });
                AnsiConsole.MarkupLine("\n[green]👋 Au revoir![/]\n");
                Environment.Exit(0);
                break;
        }
    }

    // Connexion utilisateur ou admin
    private async Task LoginAsync()
    {
        Console.Clear();
        AnsiConsole.Write(new Panel("[yellow]🔐 Connexion[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(new TextPrompt<string>("[orange3]Mot de passe:[/]").Secret());

        await AnsiConsole.Status().StartAsync("[yellow]Vérification...[/]", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            await Task.Delay(500);

            // Vérification admin hardcodé
            if (username.ToLower() == ADMIN_USERNAME && password == ADMIN_PASSWORD)
            {
                _currentUser = new User 
                { 
                    Id = -1, 
                    Username = "admin", 
                    PasswordHash = "",
                    CreatedAt = DateTime.UtcNow 
                };
            }
            else
            {
                _currentUser = await _authService.LoginAsync(username, password);
            }
        });

        if (_currentUser != null)
        {
            if (_currentUser.Id == -1)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] [bold orange3]Bienvenue, Administrateur![/] 👑");
            }
            else
            {
                int unreadCount = await GetUnreadMessageCountAsync();
                
                if (unreadCount > 0)
                {
                    AnsiConsole.Write(new Panel(new Markup($"[yellow bold]🔔 Vous avez {unreadCount} nouveau(x) message(s)![/]"))
                    {
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(foreground: Color.Yellow)
                    });
                }
                
                AnsiConsole.MarkupLine($"\n[green]✓[/] Bienvenue, [bold orange3]{_currentUser.Username}[/]!");
                _notificationCancellation = new CancellationTokenSource();
            }
            await Task.Delay(2000);
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Identifiants incorrects.[/]");
            await Task.Delay(2000);
        }

        Console.Clear();
    }

    // Inscription nouvel utilisateur
    private async Task RegisterAsync()
    {
        Console.Clear();
        AnsiConsole.Write(new Panel("[yellow]📝 Inscription[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Choisir un nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(new TextPrompt<string>("[orange3]Choisir un mot de passe (min. 6 caractères):[/]").Secret());
        var confirmPassword = AnsiConsole.Prompt(new TextPrompt<string>("[orange3]Confirmer le mot de passe:[/]").Secret());

        if (password != confirmPassword)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Les mots de passe ne correspondent pas.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        User? user = null;
        await AnsiConsole.Status().StartAsync("[yellow]Création du compte...[/]", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            await Task.Delay(500);
            user = await _authService.RegisterAsync(username, password);
        });

        AnsiConsole.MarkupLine(user != null 
            ? "\n[green]✓ Compte créé avec succès![/] Vous pouvez maintenant vous connecter."
            : "\n[red]✗ Erreur: Ce nom d'utilisateur existe déjà ou est invalide.[/]");
        
        await Task.Delay(2000);
        Console.Clear();
    }

    // Menu principal avec badge de notifications
    private async Task ShowMainMenuAsync()
    {
        int unreadCount = await GetUnreadMessageCountAsync();
        string notification = unreadCount > 0 ? $" [yellow bold]🔔 {unreadCount}[/]" : "";
        
        var choices = _currentUser!.Id == -1
            ? new[] { "👥 Gérer les utilisateurs", "➕ Ajouter un utilisateur", "📊 Statistiques", "🔄 Rafraîchir", "🚪 Se déconnecter" }
            : new[] { "📨 Envoyer un message", $"📥 Messages reçus{(unreadCount > 0 ? $" [yellow]({unreadCount})[/]" : "")}", 
                     "📤 Messages envoyés", "👥 Liste des utilisateurs", "🔄 Rafraîchir", "🚪 Se déconnecter" };

        var title = _currentUser.Id == -1 
            ? $"[yellow]═══[/] [red bold]ADMIN[/] [yellow]═══[/]{notification}"
            : $"[yellow]═══[/] [orange3 bold]{_currentUser.Username}[/] [yellow]═══[/]{notification}";

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(choices));

        Console.Clear();

        if (_currentUser.Id == -1)
            await HandleAdminActionAsync(choice);
        else
            await HandleUserActionAsync(choice);
    }

    // Actions admin
    private async Task HandleAdminActionAsync(string choice)
    {
        switch (choice)
        {
            case "👥 Gérer les utilisateurs":
                await ManageUsersAsync();
                break;
            case "➕ Ajouter un utilisateur":
                await AddUserAsync();
                break;
            case "📊 Statistiques":
                await ViewStatisticsAsync();
                break;
            case "🔄 Rafraîchir":
                AnsiConsole.MarkupLine("[yellow]🔄 Actualisation...[/]");
                await Task.Delay(500);
                Console.Clear();
                break;
            case "🚪 Se déconnecter":
                _currentUser = null;
                AnsiConsole.MarkupLine("[green]✓ Déconnexion réussie.[/]");
                await Task.Delay(1000);
                Console.Clear();
                await ShowAnimatedWelcomeAsync();
                break;
        }
    }

    // Actions utilisateur
    private async Task HandleUserActionAsync(string choice)
    {
        var cleanChoice = choice.Split('[')[0].Trim();
        
        switch (cleanChoice)
        {
            case "📨 Envoyer un message":
                await SendMessageAsync();
                break;
            case "📥 Messages reçus":
                await ViewReceivedMessagesAsync();
                break;
            case "📤 Messages envoyés":
                await ViewSentMessagesAsync();
                break;
            case "👥 Liste des utilisateurs":
                await ViewUsersAsync();
                break;
            case "🔄 Rafraîchir":
                AnsiConsole.MarkupLine("[yellow]🔄 Actualisation...[/]");
                await Task.Delay(500);
                Console.Clear();
                break;
            case "🚪 Se déconnecter":
                _notificationCancellation?.Cancel();
                _currentUser = null;
                AnsiConsole.MarkupLine("[green]✓ Déconnexion réussie.[/]");
                await Task.Delay(1000);
                Console.Clear();
                await ShowAnimatedWelcomeAsync();
                break;
        }
    }

    private async Task SendMessageAsync()
    {
        AnsiConsole.Write(new Panel("[yellow]📨 Envoyer un message[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        var receiver = AnsiConsole.Ask<string>("[orange3]Destinataire (nom d'utilisateur):[/]");
        var content = AnsiConsole.Ask<string>("[orange3]Message:[/]");

        Message? message = null;
        await AnsiConsole.Status().StartAsync("[yellow]Envoi en cours...[/]", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            message = await _messageService.SendMessageAsync(_currentUser!.Id, receiver, content);
            await Task.Delay(500);
        });

        if (message != null)
        {
            AnsiConsole.MarkupLine($"\n[green]✓ Message envoyé à[/] [orange3 bold]{receiver}[/]! 🚀");
            AnsiConsole.MarkupLine("[dim]Le destinataire peut rafraîchir son menu pour voir le message.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Utilisateur introuvable.[/]");
        }

        await Task.Delay(2500);
        Console.Clear();
    }

    // Affiche les messages reçus et les marque comme lus
    private async Task ViewReceivedMessagesAsync()
    {
        var messages = await _messageService.GetReceivedMessagesAsync(_currentUser!.Id);

        AnsiConsole.Write(new Panel("[yellow]📥 Messages reçus[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]📭 Aucun message.[/]");
            AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
            Console.ReadLine();
            Console.Clear();
            return;
        }

        var table = new Table { Border = TableBorder.Rounded };
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("De");
        table.AddColumn("Message");
        table.AddColumn("Date");
        table.AddColumn(new TableColumn("Statut").Centered());

        foreach (var msg in messages)
        {
            var statusIcon = msg.IsRead ? "[dim]✓ Lu[/]" : "[yellow bold]● Nouveau[/]";
            table.AddRow(
                msg.Id.ToString(),
                $"[orange3]{msg.SenderUsername}[/]",
                msg.DecryptedContent.Length > 50 ? msg.DecryptedContent[..47] + "..." : msg.DecryptedContent,
                msg.SentAt.ToLocalTime().ToString("dd/MM HH:mm"),
                statusIcon
            );
        }

        AnsiConsole.Write(table);

        // Marquer automatiquement comme lus
        var unreadMessages = messages.Where(m => !m.IsRead).ToList();
        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
                await _messageService.MarkAsReadAsync(msg.Id, _currentUser.Id);
            
            AnsiConsole.MarkupLine($"\n[green]✓ {unreadMessages.Count} message(s) marqué(s) comme lu(s).[/]");
        }

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices("🔄 Rafraîchir", "↩️ Retour au menu"));

        if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ViewReceivedMessagesAsync();
            return;
        }

        Console.Clear();
    }

    private async Task ViewSentMessagesAsync()
    {
        var messages = await _messageService.GetSentMessagesAsync(_currentUser!.Id);

        AnsiConsole.Write(new Panel("[yellow]📤 Messages envoyés[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]📭 Aucun message envoyé.[/]");
            AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
            Console.ReadLine();
            Console.Clear();
            return;
        }

        var table = new Table { Border = TableBorder.Rounded };
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("À");
        table.AddColumn("Message");
        table.AddColumn("Date");

        foreach (var msg in messages)
        {
            table.AddRow(
                msg.Id.ToString(),
                $"[orange3]{msg.ReceiverUsername}[/]",
                msg.DecryptedContent.Length > 50 ? msg.DecryptedContent[..47] + "..." : msg.DecryptedContent,
                msg.SentAt.ToLocalTime().ToString("dd/MM HH:mm")
            );
        }

        AnsiConsole.Write(table);

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices("✏️ Modifier un message", "🗑️ Supprimer un message", "🔄 Rafraîchir", "↩️ Retour"));

        if (action == "✏️ Modifier un message")
        {
            var msgId = AnsiConsole.Ask<int>("[orange3]ID du message:[/]");
            var newContent = AnsiConsole.Ask<string>("[orange3]Nouveau contenu:[/]");
            var success = await _messageService.UpdateMessageAsync(msgId, _currentUser.Id, newContent);
            AnsiConsole.MarkupLine(success ? "[green]✓ Message modifié.[/]" : "[red]✗ Échec.[/]");
            await Task.Delay(1500);
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else if (action == "🗑️ Supprimer un message")
        {
            var msgId = AnsiConsole.Ask<int>("[orange3]ID du message:[/]");
            var success = await _messageService.DeleteMessageAsync(msgId, _currentUser.Id);
            AnsiConsole.MarkupLine(success ? "[green]✓ Message supprimé.[/]" : "[red]✗ Échec.[/]");
            await Task.Delay(1500);
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else
        {
            Console.Clear();
        }
    }

    private async Task ViewUsersAsync()
    {
        var users = await _messageService.GetAllUsersAsync();

        AnsiConsole.Write(new Panel("[yellow]👥 Utilisateurs enregistrés[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });
        AnsiConsole.WriteLine();

        var table = new Table { Border = TableBorder.Rounded };
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Nom d'utilisateur");
        table.AddColumn("Date d'inscription");

        foreach (var user in users)
        {
            var username = user.Id == _currentUser!.Id 
                ? $"[orange3 bold]{user.Username}[/] [yellow](vous)[/]"
                : user.Username;

            table.AddRow(user.Id.ToString(), username, user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
        Console.ReadLine();
        Console.Clear();
    }

    // Gestion des utilisateurs (admin uniquement)
    private async Task ManageUsersAsync()
    {
        var users = await _messageService.GetAllUsersAsync();
        var userRepo = new Data.UserRepository();

        AnsiConsole.Write(new Panel("[red bold]👥 Gestion des utilisateurs (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        });
        AnsiConsole.WriteLine();

        var table = new Table { Border = TableBorder.Rounded };
        table.BorderColor(Color.Red);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Nom d'utilisateur");
        table.AddColumn("Date d'inscription");
        table.AddColumn(new TableColumn("Créé par").Centered());

        foreach (var user in users)
        {
            var createdBy = user.PasswordHash.StartsWith("ADMIN_") ? "[red]Admin[/]" : "[dim]User[/]";
            table.AddRow(
                user.Id.ToString(),
                user.Username,
                user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                createdBy
            );
        }

        AnsiConsole.Write(table);

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Red, decoration: Decoration.Bold))
                .AddChoices("✏️ Modifier un utilisateur (créé par admin)", 
                           "🗑️ Supprimer un utilisateur (créé par admin)", 
                           "🔄 Rafraîchir", "↩️ Retour"));

        if (action == "✏️ Modifier un utilisateur (créé par admin)")
        {
            await ModifyAdminUserAsync(users, userRepo);
        }
        else if (action == "🗑️ Supprimer un utilisateur (créé par admin)")
        {
            await DeleteAdminUserAsync(users, userRepo);
        }
        else if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ManageUsersAsync();
            return;
        }

        Console.Clear();
    }

    // Modifier un utilisateur créé par admin
    private async Task ModifyAdminUserAsync(System.Collections.Generic.List<User> users, Data.UserRepository userRepo)
    {
        var userId = AnsiConsole.Ask<int>("[orange3]ID de l'utilisateur:[/]");
        var userToModify = users.FirstOrDefault(u => u.Id == userId);
        
        if (userToModify == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Utilisateur introuvable.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        if (!userToModify.PasswordHash.StartsWith("ADMIN_"))
        {
            AnsiConsole.MarkupLine("[red]✗ Vous ne pouvez modifier que les utilisateurs créés par l'admin.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        var newUsername = AnsiConsole.Confirm("Modifier le nom d'utilisateur?") 
            ? AnsiConsole.Ask<string>($"[orange3]Nouveau nom (actuel: {userToModify.Username}):[/]")
            : userToModify.Username;
        
        var newHash = userToModify.PasswordHash;
        
        if (AnsiConsole.Confirm("Modifier le mot de passe?"))
        {
            var newPassword = AnsiConsole.Prompt(new TextPrompt<string>("[orange3]Nouveau mot de passe (min. 6 caractères):[/]").Secret());
            
            if (newPassword.Length >= 6)
            {
                newHash = "ADMIN_" + Security.PasswordHasher.HashPassword(newPassword);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Mot de passe trop court. Modification annulée.[/]");
                await Task.Delay(2000);
                Console.Clear();
                return;
            }
        }

        var success = await userRepo.UpdateUserAsync(userId, newUsername, newHash);
        AnsiConsole.MarkupLine(success 
            ? $"[green]✓ Utilisateur {newUsername} modifié avec succès![/]"
            : "[red]✗ Erreur lors de la modification (nom d'utilisateur existe déjà?).[/]");
        
        await Task.Delay(2000);
    }

    // Supprimer un utilisateur créé par admin
    private async Task DeleteAdminUserAsync(System.Collections.Generic.List<User> users, Data.UserRepository userRepo)
    {
        var userId = AnsiConsole.Ask<int>("[orange3]ID de l'utilisateur à supprimer:[/]");
        var userToDelete = users.FirstOrDefault(u => u.Id == userId);
        
        if (userToDelete == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Utilisateur introuvable.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        if (!userToDelete.PasswordHash.StartsWith("ADMIN_"))
        {
            AnsiConsole.MarkupLine("[red]✗ Vous ne pouvez supprimer que les utilisateurs créés par l'admin.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        if (AnsiConsole.Confirm($"[red]Confirmer la suppression de {userToDelete.Username}?[/]"))
        {
            var success = await userRepo.DeleteUserAsync(userId);
            AnsiConsole.MarkupLine(success 
                ? $"[green]✓ Utilisateur {userToDelete.Username} supprimé avec succès.[/]"
                : "[red]✗ Erreur lors de la suppression.[/]");
            await Task.Delay(2000);
        }
    }

    private async Task AddUserAsync()
    {
        AnsiConsole.Write(new Panel("[red bold]➕ Ajouter un utilisateur (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        });
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(new TextPrompt<string>("[orange3]Mot de passe (min. 6 caractères):[/]").Secret());

        if (password.Length < 6)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Le mot de passe doit contenir au moins 6 caractères.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        var passwordHash = "ADMIN_" + Security.PasswordHasher.HashPassword(password);
        var userRepo = new Data.UserRepository();
        
        User? user = null;
        await AnsiConsole.Status().StartAsync("[yellow]Création de l'utilisateur...[/]", async ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            user = await userRepo.CreateUserAsync(username, passwordHash);
            await Task.Delay(500);
        });

        AnsiConsole.MarkupLine(user != null 
            ? $"\n[green]✓ Utilisateur {username} créé avec succès![/]"
            : "\n[red]✗ Erreur: Ce nom d'utilisateur existe déjà.[/]");

        await Task.Delay(2000);
        Console.Clear();
    }

    private async Task ViewStatisticsAsync()
    {
        var users = await _messageService.GetAllUsersAsync();
        var adminCreatedUsers = users.Count(u => u.PasswordHash.StartsWith("ADMIN_"));
        var userCreatedUsers = users.Count - adminCreatedUsers;

        AnsiConsole.Write(new Panel("[red bold]📊 Statistiques du système (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        });
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Panel(new Markup(
                $"[orange3]👥 Utilisateurs totaux:[/] [white bold]{users.Count}[/]\n" +
                $"[red]├─ Créés par admin:[/] [white]{adminCreatedUsers}[/]\n" +
                $"[green]└─ Auto-inscrits:[/] [white]{userCreatedUsers}[/]\n\n" +
                $"[green]✓ Système opérationnel[/]"))
        {
            Header = new PanelHeader("📈 Tableau de bord", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        });

        AnsiConsole.WriteLine("\n\n[yellow]Liste complète des utilisateurs:[/]");
        var table = new Table { Border = TableBorder.Rounded };
        table.BorderColor(Color.Red);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Utilisateur");
        table.AddColumn("Inscrit le");
        table.AddColumn(new TableColumn("Type").Centered());

        foreach (var user in users)
        {
            var userType = user.PasswordHash.StartsWith("ADMIN_") ? "[red]Admin[/]" : "[green]User[/]";
            table.AddRow(
                user.Id.ToString(),
                user.Username,
                user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                userType
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
        Console.ReadLine();
        Console.Clear();
    }
}