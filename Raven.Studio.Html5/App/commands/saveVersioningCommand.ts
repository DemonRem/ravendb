﻿import database = require("models/database");
import document = require("models/document");
import commandBase = require("commands/commandBase");
import saveBulkOfDocuments = require("commands/saveBulkOfDocuments");

class saveVersioningCommand extends commandBase {
    constructor(private versioningEntries: Array<versioningEntryDto>, private removeEntries: Array<versioningEntryDto>, private db: database) {
        super();
    }


    execute(): JQueryPromise<any> {
        var commands: bulkDocumentDto[] = [];

        for (var i = 0; i < this.versioningEntries.length; i++) {
            var entry: document = new document(this.versioningEntries[i]);
            commands.push({
                Key: "Raven/Versioning/" + entry["Id"],
                Method: "PUT",
                Document: entry.toDto(false),
                Metadata: entry.__metadata.toDto(),
                Etag: entry.__metadata.etag
            });
        }

        for (var i = 0; i < this.removeEntries.length; i++) {
            var entry: document = new document(this.removeEntries[i]);
            commands.push({
                Key: "Raven/Versioning/" + entry["Id"],
                Method: "DELETE",
                Etag: entry.__metadata.etag
            });
        }

        var saveTask = new saveBulkOfDocuments("versioning", commands, this.db).execute();
        return saveTask;
    }
}

export = saveVersioningCommand;