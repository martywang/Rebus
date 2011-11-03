﻿using System;
using System.Linq;
using System.Transactions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Rebus.MongoDb
{
    public class MongoDbSubscriptionStorage : IStoreSubscriptions
    {
        readonly string collectionName;
        readonly MongoDatabase database;

        public MongoDbSubscriptionStorage(string connectionString, string collectionName)
        {
            this.collectionName = collectionName;
            database = MongoDatabase.Create(connectionString);
        }

        public void Save(Type messageType, string subscriberInputQueue)
        {
            var collection = database.GetCollection(collectionName);

            if (Transaction.Current != null)
            {
                var hack = new AmbientTxHack(() => DoUpdate(messageType, subscriberInputQueue, collection));
                Transaction.Current.EnlistVolatile(hack, EnlistmentOptions.None);
            }
            else
            {
                DoUpdate(messageType, subscriberInputQueue, collection);
            }
        }

        SafeModeResult DoUpdate(Type messageType, string subscriberInputQueue, MongoCollection<BsonDocument> collection)
        {
            return collection.Update(Query.EQ("_id", messageType.FullName),
                                     Update.AddToSet("endpoints", subscriberInputQueue),
                                     UpdateFlags.Upsert,
                                     SafeMode.True);
        }

        public string[] GetSubscribers(Type messageType)
        {
            var collection = database.GetCollection(collectionName);
            
            var doc = collection.FindOne(Query.EQ("_id", messageType.FullName)).AsBsonDocument;
            if (doc == null) return new string[0];

            var endpoints = doc["endpoints"].AsBsonArray;
            return endpoints.Values.Select(v => v.ToString()).ToArray();
        }
    }
}