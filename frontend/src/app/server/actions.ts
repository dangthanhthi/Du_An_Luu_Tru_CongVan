'use server'

import { notFound } from 'next/navigation'

import type { db as eCommerceData } from '@/fake-db/apps/ecommerce'
import type { db as academyData } from '@/fake-db/apps/academy'
import type { db as vehicleData } from '@/fake-db/apps/logistics'
import type { db as invoiceData } from '@/fake-db/apps/invoice'
import type { db as userData } from '@/fake-db/apps/userList'
import type { db as permissionData } from '@/fake-db/apps/permissions'
import type { db as profileData } from '@/fake-db/pages/userProfile'
import type { db as faqData } from '@/fake-db/pages/faq'
import type { db as pricingData } from '@/fake-db/pages/pricing'
import type { db as statisticsData } from '@/fake-db/pages/widgetExamples'

export const getEcommerceData = async (): Promise<typeof eCommerceData> => notFound()

export const getAcademyData = async (): Promise<typeof academyData> => notFound()

export const getLogisticsData = async (): Promise<typeof vehicleData> => notFound()

export const getInvoiceData = async (): Promise<typeof invoiceData> => notFound()

export const getUserData = async (): Promise<typeof userData> => notFound()

export const getPermissionsData = async (): Promise<typeof permissionData> => notFound()

export const getProfileData = async (): Promise<typeof profileData> => notFound()

export const getFaqData = async (): Promise<typeof faqData> => notFound()

export const getPricingData = async (): Promise<typeof pricingData> => notFound()

export const getStatisticsData = async (): Promise<typeof statisticsData> => notFound()
