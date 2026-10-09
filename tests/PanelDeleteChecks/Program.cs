using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Net;
using System.Net.Http;
using KtuDeYasPortal.Panel.Application.UseCases;
using KtuDeYasPortal.Panel.Components.Pages;
using KtuDeYasPortal.Panel.Domain.Entities;
using KtuDeYasPortal.Panel.Domain.Interfaces;

const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
var type = typeof(Structures);
var checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
foreach (var error in new Exception[] { new HttpRequestException("HTTP 500", null, HttpStatusCode.InternalServerError), new HttpRequestException("Connection refused") })
{
    var page = new Structures();
    var repository = new FailingRepository(error);
    type.GetProperty("StructureUC", flags)!.SetValue(page, new StructureUseCases(repository, null!));
    var id = Guid.NewGuid();
    type.GetMethod("ConfirmDelete", flags)!.Invoke(page, [id]);
    await (Task)type.GetMethod("DoDeleteAsync", flags)!.Invoke(page, null)!;
    Check(type.GetField("_deleteError", flags)!.GetValue(page) is string message && message.Contains("Yapı silinemedi"), "Action returns with visible error");
    Check((Guid?)type.GetField("_deletingId", flags)!.GetValue(page) == id, "Failed deletion keeps dialog and structure");
    Check((bool)type.GetField("_isSaving", flags)!.GetValue(page)! == false, "Spinner and disabled state reset");
    await (Task)type.GetMethod("DoDeleteAsync", flags)!.Invoke(page, null)!;
    Check(repository.Calls == 2, "Action can be retried without circuit restart");
}
Console.WriteLine($"{checks} panel deletion failure/retry checks passed.");

sealed class FailingRepository(Exception error) : IStructureRepository
{
    public int Calls;
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { Calls++; return Task.FromException(error); }
    public Task<List<Structure>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Structure?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Structure> CreateAsync(Structure s, List<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Structure> UpdateAsync(Guid id, Structure s, List<Guid> ids, CancellationToken ct = default) => throw new NotSupportedException();
}
