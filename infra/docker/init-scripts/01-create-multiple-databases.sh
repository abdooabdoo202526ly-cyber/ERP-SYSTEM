#!/bin/bash
#
# ينشئ قواعد بيانات إضافية لما يحددها المتغير POSTGRES_MULTIPLE_DATABASES
# صيغة: "db1:db2:db3" — يضمن وجود الـ user مالك لهذه الـ databases
#

set -e

function create_user_and_database() {
    local database=$1
    echo "  → إنشاء قاعدة البيانات: $database"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
        CREATE DATABASE "$database";
        GRANT ALL PRIVILEGES ON DATABASE "$database" TO "$POSTGRES_USER";
EOSQL
}

if [ -n "$POSTGRES_MULTIPLE_DATABASES" ]; then
    echo "إنشاء قواعد بيانات إضافية: $POSTGRES_MULTIPLE_DATABASES"
    for db in $(echo "$POSTGRES_MULTIPLE_DATABASES" | tr ':' ' '); do
        create_user_and_database "$db"
    done
    echo "تم إنشاء جميع قواعد البيانات بنجاح."
fi
