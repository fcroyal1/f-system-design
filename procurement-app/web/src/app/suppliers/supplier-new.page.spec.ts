import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { SupplierNewPage } from './supplier-new.page';
import { ReferenceData } from '../core/models';
import { problem } from '../testing/fixtures';

@Component({ template: '' })
class SuppliersStub {}

const reference: ReferenceData = {
  categories: [{ code: 'MED-SUPPLIES', name: 'Medical supplies', isPoExempt: false }],
  glAccounts: [],
  costCenters: [],
  doaTiers: [],
};

function type(el: HTMLInputElement | HTMLSelectElement, value: string): void {
  el.value = value;
  el.dispatchEvent(new Event(el instanceof HTMLSelectElement ? 'change' : 'input'));
}

describe('SupplierNewPage', () => {
  it('SupplierNewPage clears tax and bank fields after submit', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'suppliers', component: SuppliersStub }]),
      ],
    });
    const http = TestBed.inject(HttpTestingController);
    const router = TestBed.inject(Router);

    const fixture = TestBed.createComponent(SupplierNewPage);
    fixture.detectChanges();
    http.expectOne({ method: 'GET', url: '/api/reference' }).flush(reference);
    await fixture.whenStable();

    const page = fixture.nativeElement as HTMLElement;
    const field = <T extends Element>(name: string) => page.querySelector<T>(`[formcontrolname="${name}"]`)!;
    const taxId = field<HTMLInputElement>('taxId');
    const bank = field<HTMLInputElement>('bankAccount');

    const fill = async (code: string) => {
      type(field<HTMLInputElement>('code'), code);
      type(field<HTMLInputElement>('legalName'), 'Northwind Lab Goods');
      type(field<HTMLSelectElement>('categoryCode'), 'MED-SUPPLIES');
      type(field<HTMLInputElement>('paymentTermsDays'), '45');
      type(taxId, '98-7654421');
      type(bank, 'GB29 NWBK 6016 1331 9268 19');
      await fixture.whenStable();
    };
    const submit = () => page.querySelector('form')!.dispatchEvent(new Event('submit'));
    const expected = (code: string) => ({
      code,
      legalName: 'Northwind Lab Goods',
      categoryCode: 'MED-SUPPLIES',
      paymentTermsDays: 45,
      taxId: '98-7654421',
      bankAccount: 'GB29 NWBK 6016 1331 9268 19',
    });

    // 409: the full numbers were sent, the page stays, and the fields are blank.
    await fill('V-10442');
    submit();
    const conflict = http.expectOne({ method: 'POST', url: '/api/suppliers' });
    expect(conflict.request.body).toEqual(expected('V-10442'));
    const { body, init } = problem(409, 'Conflict', 'A supplier with code V-10442 already exists.');
    conflict.flush(body, init);
    await fixture.whenStable();

    expect(taxId.value).toBe('');
    expect(bank.value).toBe('');
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('A supplier with code V-10442 already exists.');
    expect(router.url).toBe('/');

    // 201: the full numbers were sent, and the fields are blank before the page navigates away.
    await fill('SUP-900');
    submit();
    const created = http.expectOne({ method: 'POST', url: '/api/suppliers' });
    expect(created.request.body).toEqual(expected('SUP-900'));
    expect(taxId.value).toBe('');
    expect(bank.value).toBe('');
    created.flush(
      {
        id: '22222222-0000-4000-8000-000000000900',
        code: 'SUP-900',
        legalName: 'Northwind Lab Goods',
        categoryCode: 'MED-SUPPLIES',
        categoryName: 'Medical supplies',
        paymentTermsDays: 45,
        taxIdMasked: '•••• 4421',
        bankMasked: '•••• 6819',
        isBlocked: false,
      },
      { status: 201, statusText: 'Created' },
    );
    await fixture.whenStable();

    expect(taxId.value).toBe('');
    expect(bank.value).toBe('');
    expect(router.url).toBe('/suppliers');

    http.verify();
  });
});
