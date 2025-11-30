import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RequestDelete } from './request-delete';

describe('RequestDelete', () => {
  let component: RequestDelete;
  let fixture: ComponentFixture<RequestDelete>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RequestDelete]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RequestDelete);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
