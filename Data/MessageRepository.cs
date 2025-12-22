using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using EncryptedMessaging.Models;
using EncryptedMessaging.Security;

namespace EncryptedMessaging.Data;

public class MessageRepository
{
    // Ouvre une connexion SQLite
    private static async Task<SQLiteConnection> OpenConnectionAsync()
    {
        var conn = new SQLiteConnection(DatabaseInitializer.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    // Crée un message chiffré
    public async Task<Message?> CreateMessageAsync(int senderId, int receiverId, string content)
    {
        var encryptedContent = AesEncryption.Encrypt(content);
        var sentAt = DateTime.UtcNow;

        using var conn = await OpenConnectionAsync();
        using var cmd = new SQLiteCommand(@"
            INSERT INTO Messages (SenderId, ReceiverId, EncryptedContent, SentAt, IsRead)
            VALUES (@SenderId, @ReceiverId, @EncryptedContent, @SentAt, 0);
            SELECT last_insert_rowid();", conn);

        cmd.Parameters.AddWithValue("@SenderId", senderId);
        cmd.Parameters.AddWithValue("@ReceiverId", receiverId);
        cmd.Parameters.AddWithValue("@EncryptedContent", encryptedContent);
        cmd.Parameters.AddWithValue("@SentAt", sentAt.ToString("o"));

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return new Message
        {
            Id = id,
            SenderId = senderId,
            ReceiverId = receiverId,
            EncryptedContent = encryptedContent,
            DecryptedContent = content,
            SentAt = sentAt,
            IsRead = false
        };
    }

    // Messages reçus par un utilisateur
    public Task<List<Message>> GetReceivedMessagesAsync(int userId) =>
        GetMessagesAsync(
            @"SELECT m.*, u.Username
              FROM Messages m
              JOIN Users u ON m.SenderId = u.Id
              WHERE m.ReceiverId = @UserId
              ORDER BY m.SentAt DESC",
            userId,
            isInbox: true
        );

    // Messages envoyés par un utilisateur
    public Task<List<Message>> GetSentMessagesAsync(int userId) =>
        GetMessagesAsync(
            @"SELECT m.*, u.Username
              FROM Messages m
              JOIN Users u ON m.ReceiverId = u.Id
              WHERE m.SenderId = @UserId
              ORDER BY m.SentAt DESC",
            userId,
            isInbox: false
        );

    // Lecture des messages (méthode commune)
    private static async Task<List<Message>> GetMessagesAsync(string query, int userId, bool isInbox)
    {
        var messages = new List<Message>();

        using var conn = await OpenConnectionAsync();
        using var cmd = new SQLiteCommand(query, conn);
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var encrypted = reader.GetString(3);

            messages.Add(new Message
            {
                Id = reader.GetInt32(0),
                SenderId = reader.GetInt32(1),
                ReceiverId = reader.GetInt32(2),
                EncryptedContent = encrypted,
                DecryptedContent = AesEncryption.Decrypt(encrypted),
                SentAt = DateTime.Parse(reader.GetString(4)),
                IsRead = reader.GetInt32(5) == 1,
                SenderUsername = isInbox ? reader.GetString(6) : null,
                ReceiverUsername = !isInbox ? reader.GetString(6) : null
            });
        }

        return messages;
    }

    // Mise à jour d’un message (par l’expéditeur)
    public async Task<bool> UpdateMessageAsync(int messageId, int senderId, string newContent)
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new SQLiteCommand(@"
            UPDATE Messages
            SET EncryptedContent = @Content
            WHERE Id = @Id AND SenderId = @SenderId", conn);

        cmd.Parameters.AddWithValue("@Content", AesEncryption.Encrypt(newContent));
        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@SenderId", senderId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Suppression d’un message (par l’expéditeur)
    public async Task<bool> DeleteMessageAsync(int messageId, int senderId)
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new SQLiteCommand(
            "DELETE FROM Messages WHERE Id = @Id AND SenderId = @SenderId", conn);

        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@SenderId", senderId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Marque un message comme lu
    public async Task<bool> MarkAsReadAsync(int messageId, int receiverId)
    {
        using var conn = await OpenConnectionAsync();
        using var cmd = new SQLiteCommand(@"
            UPDATE Messages
            SET IsRead = 1
            WHERE Id = @Id AND ReceiverId = @ReceiverId", conn);

        cmd.Parameters.AddWithValue("@Id", messageId);
        cmd.Parameters.AddWithValue("@ReceiverId", receiverId);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
