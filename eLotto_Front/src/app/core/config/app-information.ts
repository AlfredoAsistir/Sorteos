import { APP_BRANDING } from '../../config/branding.config';

export interface AppInformation {
  appTitle: string;
  appLocation: string;
  appEmail: string;
  urlEmail: string;
  urlFacebook: string;
  urlWhatsApp: string;
  urlSite: string;
  appLogo: string;
}

export const appInformation: Readonly<AppInformation> = {
  appTitle: APP_BRANDING.name,
  appLocation: APP_BRANDING.location,
  appEmail: APP_BRANDING.contact.email,
  urlEmail: APP_BRANDING.contact.emailUrl,
  urlFacebook: APP_BRANDING.social.facebookUrl,
  urlWhatsApp: APP_BRANDING.contact.whatsappUrl,
  urlSite: APP_BRANDING.siteUrl,
  appLogo: APP_BRANDING.logoUrl
};
