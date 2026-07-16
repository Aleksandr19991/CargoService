CREATE USER keycloak WITH PASSWORD 'keycloak';
CREATE DATABASE keycloak OWNER keycloak;
GRANT ALL PRIVILEGES ON DATABASE keycloak TO keycloak;

CREATE USER cargoservice WITH PASSWORD 'cargoservice';
CREATE DATABASE cargoservice OWNER cargoservice;
GRANT ALL PRIVILEGES ON DATABASE cargoservice TO cargoservice;
