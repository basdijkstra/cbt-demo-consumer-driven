import path from 'path';
import { MatchersV3, PactV4 } from '@pact-foundation/pact';
import {
  CustomerClient,
  CustomerNotFoundError,
  InvalidCustomerIdError,
} from '../src/customerClient';

const { integer, string, regex } = MatchersV3;

const pact = new PactV4({
  consumer: 'OSCustomerConsumer',
  provider: 'SAPCustomerProvider',
  // Shared folder in the repository root, published from there to PactFlow (npm run pact:publish)
  dir: path.resolve(__dirname, '..', '..', 'pacts'),
  logLevel: 'warn',
});

describe('CustomerClient', () => {
  describe('GET /customers/{id}', () => {
    it('returns the customer when the customer exists', async () => {
      await pact
        .addInteraction()
        .given('customer 12345 exists')
        .uponReceiving('a request for an existing customer')
        .withRequest('GET', '/customers/12345', (builder) => {
          builder.headers({ Accept: 'application/json' });
        })
        .willRespondWith(200, (builder) => {
          // Only the fields this consumer actually uses get expectations.
          // email, phone and lastModified are deliberately left out of the contract.
          builder.jsonBody({
            id: integer(12345),
            name: string('Susan Jones'),
            status: regex('^(ACTIVE|INACTIVE)$', 'ACTIVE'),
            address: {
              street: string('Main Street'),
              houseNumber: integer(15),
              city: string('New York'),
              zipCode: string('Y1A 2B3'),
            },
          });
        })
        .executeTest(async (mockServer) => {
          const client = new CustomerClient(mockServer.url);

          const customer = await client.getCustomer(12345);

          expect(customer.id).toBe(12345);
          expect(customer.name).toBe('Susan Jones');
          expect(customer.status).toBe('ACTIVE');
          expect(customer.address).toEqual({
            street: 'Main Street',
            houseNumber: 15,
            city: 'New York',
            zipCode: 'Y1A 2B3',
          });
        });
    });

    it('throws CustomerNotFoundError when the customer does not exist', async () => {
      await pact
        .addInteraction()
        .given('customer 99999 does not exist')
        .uponReceiving('a request for a non-existent customer')
        .withRequest('GET', '/customers/99999', (builder) => {
          builder.headers({ Accept: 'application/json' });
        })
        .willRespondWith(404)
        .executeTest(async (mockServer) => {
          const client = new CustomerClient(mockServer.url);

          await expect(client.getCustomer(99999)).rejects.toBeInstanceOf(CustomerNotFoundError);
        });
    });

    it('throws InvalidCustomerIdError when the customer ID is not an integer', async () => {
      await pact
        .addInteraction()
        .uponReceiving('a request for a customer with a non-integer ID')
        .withRequest('GET', '/customers/abc', (builder) => {
          builder.headers({ Accept: 'application/json' });
        })
        .willRespondWith(400)
        .executeTest(async (mockServer) => {
          const client = new CustomerClient(mockServer.url);

          await expect(client.getCustomer('abc')).rejects.toBeInstanceOf(InvalidCustomerIdError);
        });
    });
  });
});
