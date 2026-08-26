CREATE USER keycloak WITH PASSWORD 'keycloak';
CREATE DATABASE keycloak OWNER keycloak;
GRANT ALL PRIVILEGES ON DATABASE keycloak TO keycloak;

CREATE USER cargoservice WITH PASSWORD 'cargoservice';
CREATE DATABASE cargoservice OWNER cargoservice;
GRANT ALL PRIVILEGES ON DATABASE cargoservice TO cargoservice;

CREATE USER clientsservice WITH PASSWORD 'clientsservice';
CREATE DATABASE clientsservice OWNER clientsservice;
GRANT ALL PRIVILEGES ON DATABASE clientsservice TO clientsservice;

CREATE USER pricingservice WITH PASSWORD 'pricingservice';
CREATE DATABASE pricingservice OWNER pricingservice;
GRANT ALL PRIVILEGES ON DATABASE pricingservice TO pricingservice;

CREATE USER ordersservice WITH PASSWORD 'ordersservice';
CREATE DATABASE ordersservice OWNER ordersservice;
GRANT ALL PRIVILEGES ON DATABASE ordersservice TO ordersservice;

-- cargo-service. Имя cargoshipments, а не cargoservice: последнее занято identity-service
-- (наследие общего названия платформы, появившегося до этого сервиса).
CREATE USER cargoshipments WITH PASSWORD 'cargoshipments';
CREATE DATABASE cargoshipments OWNER cargoshipments;
GRANT ALL PRIVILEGES ON DATABASE cargoshipments TO cargoshipments;

CREATE USER notificationservice WITH PASSWORD 'notificationservice';
CREATE DATABASE notificationservice OWNER notificationservice;
GRANT ALL PRIVILEGES ON DATABASE notificationservice TO notificationservice;
