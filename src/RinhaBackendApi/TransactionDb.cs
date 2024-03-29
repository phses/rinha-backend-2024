﻿using Npgsql;

namespace RinhaBackendApi;

public class TransactionDb(NpgsqlDataSource _dataSource)
{
    public async Task<Result<TransacaoResp>> Add(TransacaoReq transacao, int clienteId)
    {
        using var connection = await _dataSource.OpenConnectionAsync();
        
        var existe = await CheckSeClienteExiste(clienteId, connection);

        if(!existe)
            return Result<TransacaoResp>.EntityNotFound();

        var atualizado = await UpdateClinte(transacao, clienteId, connection);

        if (!atualizado)
            return Result<TransacaoResp>.EntityNotProcessed();

        return await InsertTransacao(transacao, clienteId, connection);
    }

    private async Task<bool> CheckSeClienteExiste(int clienteId, NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand(Queries.CheckSeClienteExiste, connection);

        cmd.Parameters.AddWithValue("clienteId", clienteId);
        var resultObj = await cmd.ExecuteScalarAsync();

        long result = (resultObj as long?) ?? 0;
        return result == 1;
    }

    private async Task<bool> UpdateClinte(TransacaoReq transacao, int clienteId, NpgsqlConnection connection)
    {
        var sum = transacao.tipo == "d" ? transacao.valor * -1 : transacao.valor;

        await using var cmd = new NpgsqlCommand(Queries.UpdateSaldoCliente, connection);

        cmd.Parameters.AddWithValue("sum", sum);
        cmd.Parameters.AddWithValue("ClienteId", clienteId);


        var count = await cmd.ExecuteNonQueryAsync();

        return count > 0;
    }

    private async Task<Result<TransacaoResp>> InsertTransacao(TransacaoReq transacao, int clienteId, NpgsqlConnection connection)
    {
        await using var cmd = new NpgsqlCommand(Queries.InsereTransacao, connection);

        cmd.Parameters.AddWithValue("clienteId", clienteId);
        cmd.Parameters.AddWithValue("valor", transacao.valor);
        cmd.Parameters.AddWithValue("tipo", transacao.tipo);
        cmd.Parameters.AddWithValue("descricao", transacao.descricao);

        await cmd.ExecuteNonQueryAsync();

        await using var queryCmd = new NpgsqlCommand(Queries.GetDadosCliente, connection);
        queryCmd.Parameters.AddWithValue("clienteId", clienteId);
        using var reader = await queryCmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            int limite = reader.GetInt32(0);
            int saldo = reader.GetInt32(1);

            return new TransacaoResp(limite: limite, saldo: saldo);
        }

        return Result<TransacaoResp>.WithError();
    }
}
