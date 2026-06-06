import os
import sys
import psycopg2
from psycopg2 import sql, errors

host     = os.environ["DB_HOST"]
user     = os.environ["DB_ADMIN_USER"]
password = os.environ["DB_ADMIN_PASSWORD"]
hr_pwd   = os.environ["HR_SVC_PASSWORD"]
ob_pwd   = os.environ["ONBOARDING_SVC_PASSWORD"]

conn = psycopg2.connect(host=host, port=5432, user=user, password=password, dbname="postgres")
conn.autocommit = True
cur = conn.cursor()

def run(label, query, *args):
    try:
        cur.execute(query, *args)
        print(f"==> {label}: OK")
    except (errors.DuplicateDatabase, errors.DuplicateObject) as e:
        print(f"==> {label}: already exists, skipping")
    except Exception as e:
        print(f"ERROR {label}: {e}", file=sys.stderr)
        sys.exit(1)

# Application database + service accounts
run("CREATE DATABASE htx",
    "CREATE DATABASE htx")

run("CREATE USER hr_svc",
    sql.SQL("CREATE USER hr_svc WITH PASSWORD {}").format(sql.Literal(hr_pwd)))

run("CREATE USER onboarding_svc",
    sql.SQL("CREATE USER onboarding_svc WITH PASSWORD {}").format(sql.Literal(ob_pwd)))

run("GRANT CONNECT to hr_svc",
    "GRANT CONNECT ON DATABASE htx TO hr_svc")

run("GRANT CONNECT to onboarding_svc",
    "GRANT CONNECT ON DATABASE htx TO onboarding_svc")

# Temporal databases (auto-setup initialises the schema on first start)
run("CREATE DATABASE temporal_db",
    "CREATE DATABASE temporal_db")

run("CREATE DATABASE temporal_visibility",
    "CREATE DATABASE temporal_visibility")

run("GRANT ALL ON temporal_db",
    "GRANT ALL PRIVILEGES ON DATABASE temporal_db TO htx_svc")

run("GRANT ALL ON temporal_visibility",
    "GRANT ALL PRIVILEGES ON DATABASE temporal_visibility TO htx_svc")

print("==> Done.")
