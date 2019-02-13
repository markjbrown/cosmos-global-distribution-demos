function resolver(incomingRecord, existingRecord, isTombstone, conflictingRecords) {
    var collection = getContext().getCollection();

    if (!incomingRecord) {
        if (existingRecord) {

            collection.deleteDocument(existingRecord._self, {}, function (err, responseOptions) {
                if (err) throw err;
            });
        }
    } else if (isTombstone) {
        // delete always wins.
    } else {
        if (existingRecord) {
            if (incomingRecord.userDefinedId > existingRecord.userDefinedId) {
                return; // existing record wins
            }
        }

        var i;
        for (i = 0; i < conflictingRecords.length; i++) {
            if (incomingRecord.userDefinedId > conflictingRecords[i].userDefinedId) {
                return; // existing conflict record wins
            }
        }

        // incoming record wins - clear conflicts and replace existing with incoming.
        tryDelete(conflictingRecords, incomingRecord, existingRecord);
    }

    function tryDelete(documents, incoming, existing) {
        if (documents.length > 0) {
            collection.deleteDocument(documents[0]._self, {}, function (err, responseOptions) {
                if (err) throw err;

                documents.shift();
                tryDelete(documents, incoming, existing);
            });
        } else if (existing) {
            collection.replaceDocument(existing._self, incoming,
                function (err, documentCreated) {
                    if (err) throw err;
                });
        } else {
            collection.createDocument(collection.getSelfLink(), incoming,
                function (err, documentCreated) {
                    if (err) throw err;
                });
        }
    }
}