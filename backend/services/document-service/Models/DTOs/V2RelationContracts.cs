namespace DocumentService;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record V2RelationChange(IReadOnlyList<Guid>? AddedIds = null, IReadOnlyList<Guid>? RemovedIds = null);

// Trusted verified DetailRead scope, bound to the actor. Never accept this set from a browser.
// Live resource-policy resolution is a future caller responsibility; no EAP adapter is added.
public sealed record V2RelationScope(Guid ActorUserId, IReadOnlySet<Guid> ReadableDocumentIds);
public sealed record V2RegistrationRelations(IReadOnlyList<Guid> RelatedDocumentIds, V2RelationScope Scope);
