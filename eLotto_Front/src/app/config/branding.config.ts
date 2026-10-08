const countryCallingCode = '52';
const mobileNumber = '9982566157';
const email = 'sorteos@gbcancun.com';

export const APP_BRANDING = {
  name: 'Sorteos Global Broker',
  faviconUrl: 'assets/images/sorteos-global-broker.ico',
  logoUrl: 'assets/images/logos/logo.png',
  siteUrl: 'https://sorteos.gbcancun.com',
  location: 'Cancún, Quintana Roo',
  social: {
    facebookUrl: 'https://www.facebook.com/GlobalBrokerOficial?locale=es_LA',
  },
  contact: {
    email,
    emailUrl: 'mailto:' + email,
    whatsappUrl: 'https://wa.me/' + countryCallingCode + mobileNumber,
  },
} as const;
